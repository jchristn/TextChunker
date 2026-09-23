namespace TextChunker.Chunking
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Exceptions;
    using TextChunker.Models;
    using TextChunker.Observability;
    using TextChunker.Tokenization;

    /// <summary>
    /// Default chunker. Turns text, streams, files, lists, tables, and structured requests into an ordered stream
    /// of chunks. A single instance holds no per call mutable state and is safe to share across threads.
    /// </summary>
    public class Chunker : IChunker
    {
        private readonly ITokenizerAdapter? _ExplicitTokenizer;
        private readonly TokenizationProfileResolver _Resolver;

        /// <summary>
        /// Initialize a chunker that resolves its tokenizer from options.
        /// </summary>
        public Chunker()
            : this(null, null)
        {
        }

        /// <summary>
        /// Initialize a chunker that always uses the supplied tokenizer, ignoring tokenizer family resolution.
        /// </summary>
        /// <param name="tokenizer">Tokenizer adapter to use for all operations.</param>
        public Chunker(ITokenizerAdapter tokenizer)
            : this(tokenizer, null)
        {
        }

        /// <summary>
        /// Initialize a chunker with an optional explicit tokenizer and an optional calibration probe.
        /// </summary>
        /// <param name="tokenizer">Tokenizer adapter to use for all operations, or null to resolve from options.</param>
        /// <param name="calibrationProbe">Optional calibration probe used during tokenizer budget resolution.</param>
        public Chunker(ITokenizerAdapter? tokenizer, ITokenizerCalibrationProbe? calibrationProbe)
        {
            _ExplicitTokenizer = tokenizer;
            _Resolver = new TokenizationProfileResolver(calibrationProbe);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Chunk> ChunkText(
            string text,
            ChunkingOptions? options = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            options ??= new ChunkingOptions();
            string source = text ?? string.Empty;
            GuardInputSize(source.Length, options);

            SemanticCellRequest request = BuildTextRequest(source, options);
            await foreach (Chunk chunk in ChunkCoreAsync(request, options, source, token).ConfigureAwait(false))
                yield return chunk;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Chunk> ChunkStream(
            Stream stream,
            ChunkingOptions? options = null,
            Encoding? encoding = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            options ??= new ChunkingOptions();

            string source;
            using (StreamReader reader = new StreamReader(
                stream,
                encoding ?? Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true))
            {
                source = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            GuardInputSize(source.Length, options);
            SemanticCellRequest request = BuildTextRequest(source, options);
            await foreach (Chunk chunk in ChunkCoreAsync(request, options, source, token).ConfigureAwait(false))
                yield return chunk;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Chunk> ChunkFile(
            string filename,
            ChunkingOptions? options = null,
            FileChunkingOptions? fileOptions = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException(nameof(filename));
            if (!File.Exists(filename)) throw new FileNotFoundException("Input file not found.", filename);

            options ??= new ChunkingOptions();
            fileOptions ??= new FileChunkingOptions();

            string source;
            using (FileStream fileStream = new FileStream(
                filename,
                FileMode.Open,
                FileAccess.Read,
                MapShare(fileOptions.AccessMode)))
            using (StreamReader reader = new StreamReader(
                fileStream,
                fileOptions.Encoding,
                fileOptions.DetectEncodingFromByteOrderMarks,
                bufferSize: 1024,
                leaveOpen: false))
            {
                source = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            GuardInputSize(source.Length, options);
            SemanticCellRequest request = BuildTextRequest(source, options);
            await foreach (Chunk chunk in ChunkCoreAsync(request, options, source, token).ConfigureAwait(false))
                yield return chunk;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Chunk> ChunkList(
            IEnumerable<string> items,
            bool ordered = false,
            ChunkingOptions? options = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            options ??= new ChunkingOptions();

            List<string> list = items.ToList();
            GuardInputSize(list.Sum(i => (i ?? string.Empty).Length), options);

            SemanticCellRequest request = new SemanticCellRequest
            {
                Type = ContentTypeEnum.List,
                ParentGUID = options.ParentGUID != Guid.Empty ? options.ParentGUID : null
            };
            if (ordered) request.OrderedList = list;
            else request.UnorderedList = list;

            ChunkingOptions listOptions = WithInputType(options, ContentTypeEnum.List);
            await foreach (Chunk chunk in ChunkCoreAsync(request, listOptions, null, token).ConfigureAwait(false))
                yield return chunk;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Chunk> ChunkTable(
            IReadOnlyList<IReadOnlyList<string>> rows,
            ChunkingOptions? options = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            options ??= new ChunkingOptions();

            List<List<string>> table = rows.Select(row => row.ToList()).ToList();
            GuardInputSize(table.Sum(r => r.Sum(c => (c ?? string.Empty).Length)), options);

            ChunkingOptions tableOptions = WithInputType(options, ContentTypeEnum.Table);
            if (tableOptions.Strategy != ChunkStrategyEnum.Row
                && tableOptions.Strategy != ChunkStrategyEnum.RowWithHeaders
                && tableOptions.Strategy != ChunkStrategyEnum.RowGroupWithHeaders
                && tableOptions.Strategy != ChunkStrategyEnum.KeyValuePairs
                && tableOptions.Strategy != ChunkStrategyEnum.WholeTable)
            {
                tableOptions.Strategy = ChunkStrategyEnum.RowWithHeaders;
            }

            SemanticCellRequest request = new SemanticCellRequest
            {
                Type = ContentTypeEnum.Table,
                Table = table,
                ParentGUID = options.ParentGUID != Guid.Empty ? options.ParentGUID : null
            };

            await foreach (Chunk chunk in ChunkCoreAsync(request, tableOptions, null, token).ConfigureAwait(false))
                yield return chunk;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Chunk> ChunkRequest(
            SemanticCellRequest request,
            ChunkingOptions? options = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            options ??= new ChunkingOptions();

            ChunkingOptions nodeOptions = WithInputType(options, request.Type);
            string? offsetSource = ChunkDispatcher.IsTextType(request.Type) ? request.Text ?? string.Empty : null;

            await foreach (Chunk chunk in ChunkCoreAsync(request, nodeOptions, offsetSource, token).ConfigureAwait(false))
                yield return chunk;

            if (request.Children != null)
            {
                foreach (SemanticCellRequest child in request.Children)
                {
                    token.ThrowIfCancellationRequested();
                    await foreach (Chunk chunk in ChunkRequest(child, options, token).ConfigureAwait(false))
                        yield return chunk;
                }
            }
        }

        /// <summary>
        /// Chunk text synchronously and return the materialized chunks. The asynchronous streaming methods are the
        /// primary API; this convenience method drains the stream to a list.
        /// </summary>
        /// <param name="text">Text to chunk.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <returns>The produced chunks in order.</returns>
        public IReadOnlyList<Chunk> Chunk(string text, ChunkingOptions? options = null)
        {
            return CollectAsync(ChunkText(text, options, CancellationToken.None)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Chunk text and return the chunks together with run level diagnostics.
        /// </summary>
        /// <param name="text">Text to chunk.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A materialized result with chunks and diagnostics.</returns>
        public async Task<ChunkingResult> ChunkToResultAsync(string text, ChunkingOptions? options = null, CancellationToken token = default)
        {
            options ??= new ChunkingOptions();
            string source = text ?? string.Empty;
            GuardInputSize(source.Length, options);

            ResolvedTokenizationProfile profile = await ResolveProfileAsync(options, token).ConfigureAwait(false);
            int budget = ComputeBudget(options, profile);

            List<Chunk> chunks = await CollectAsync(ChunkText(source, options, token)).ConfigureAwait(false);

            ChunkingResult result = new ChunkingResult
            {
                Chunks = chunks,
                Diagnostic = new ChunkDiagnostic
                {
                    TokenizerKind = profile.TokenizerKind,
                    TokenizerModel = profile.TokenizerModel,
                    ProfileSource = profile.ProfileSource,
                    EffectiveTokenBudget = budget,
                    Strategy = options.Strategy,
                    ChunkCount = chunks.Count,
                    TotalTokens = chunks.Sum(c => c.TokenCount),
                    MaxChunkTokens = chunks.Count > 0 ? chunks.Max(c => c.TokenCount) : 0,
                    InputCharacterCount = source.Length
                }
            };

            return result;
        }

        private async IAsyncEnumerable<Chunk> ChunkCoreAsync(
            SemanticCellRequest request,
            ChunkingOptions options,
            string? offsetSource,
            [EnumeratorCancellation] CancellationToken token)
        {
            using Activity? activity = ChunkingActivitySource.Source.StartActivity("chunk");
            token.ThrowIfCancellationRequested();

            ResolvedTokenizationProfile profile = await ResolveProfileAsync(options, token).ConfigureAwait(false);
            ITokenizerAdapter tokenizer = _ExplicitTokenizer ?? TokenizerAdapterFactory.Create(profile);

            int budget = ComputeBudget(options, profile);
            int prefixTokens = string.IsNullOrEmpty(options.ContextPrefix) ? 0 : tokenizer.CountTokens(options.ContextPrefix!);
            int workingBudget = Math.Max(1, budget - prefixTokens);

            List<RawPiece> pieces = await Task
                .Run(() => Transform(ChunkDispatcher.Produce(request, options, tokenizer, workingBudget), options, tokenizer, workingBudget), token)
                .ConfigureAwait(false);

            Guid? parent = ResolveParent(request, options);
            bool offsetsAllowed = options.ComputeOffsets && offsetSource != null && string.IsNullOrEmpty(options.ContextPrefix);
            int searchFrom = 0;
            int position = 0;

            foreach (RawPiece piece in pieces)
            {
                token.ThrowIfCancellationRequested();

                Chunk chunk = Enrich(piece, position, parent, options, profile, tokenizer, offsetSource, offsetsAllowed, ref searchFrom);
                CopyRequestMetadata(chunk, request);
                position++;

                ChunkingMetrics.ChunksProduced.Add(1);
                if (options.ComputeTokenCounts) ChunkingMetrics.ChunkTokenCount.Record(chunk.TokenCount);

                yield return chunk;
            }
        }

        private List<RawPiece> Transform(List<RawPiece> pieces, ChunkingOptions options, ITokenizerAdapter tokenizer, int workingBudget)
        {
            List<RawPiece> working = pieces;

            if (options.TrimWhitespace)
            {
                List<RawPiece> trimmed = new List<RawPiece>(working.Count);
                foreach (RawPiece piece in working)
                {
                    string candidate = piece.Text.Trim();
                    if (candidate.Length == 0) continue;

                    // Trimming a leading space can raise the token count under byte-pair tokenizers, because a
                    // leading-space word is often a single token that splits without the space. Only apply the
                    // trim when it does not increase the token count, so the budget guarantee holds on the
                    // emitted text without losing content.
                    if (candidate.Length != piece.Text.Length
                        && tokenizer.CountTokens(candidate) > tokenizer.CountTokens(piece.Text))
                    {
                        candidate = piece.Text;
                    }

                    trimmed.Add(new RawPiece(candidate, piece.HeaderContext, piece.OffsetEligible));
                }
                working = trimmed;
            }

            if (options.MinChunkTokens > 0 && options.SmallChunkMode != SmallChunkModeEnum.Keep && working.Count > 0)
                working = ApplySmallChunkMode(working, options, tokenizer, workingBudget);

            return working;
        }

        private List<RawPiece> ApplySmallChunkMode(List<RawPiece> pieces, ChunkingOptions options, ITokenizerAdapter tokenizer, int workingBudget)
        {
            if (options.SmallChunkMode == SmallChunkModeEnum.Drop)
            {
                return pieces.Where(p => tokenizer.CountTokens(p.Text) >= options.MinChunkTokens).ToList();
            }

            List<RawPiece> merged = new List<RawPiece>();
            foreach (RawPiece piece in pieces)
            {
                if (merged.Count == 0)
                {
                    merged.Add(piece);
                    continue;
                }

                RawPiece previous = merged[merged.Count - 1];
                int previousTokens = tokenizer.CountTokens(previous.Text);
                if (previousTokens < options.MinChunkTokens)
                {
                    string candidateText = previous.Text + "\n" + piece.Text;
                    if (tokenizer.CountTokens(candidateText) <= workingBudget)
                    {
                        merged[merged.Count - 1] = new RawPiece(candidateText, previous.HeaderContext, previous.OffsetEligible && piece.OffsetEligible);
                        continue;
                    }
                }

                merged.Add(piece);
            }

            // A small final chunk has no following chunk to merge into, so absorb it backward into the previous
            // chunk when the combined size still fits the budget.
            if (merged.Count >= 2)
            {
                RawPiece last = merged[merged.Count - 1];
                if (tokenizer.CountTokens(last.Text) < options.MinChunkTokens)
                {
                    RawPiece previous = merged[merged.Count - 2];
                    string candidateText = previous.Text + "\n" + last.Text;
                    if (tokenizer.CountTokens(candidateText) <= workingBudget)
                    {
                        merged[merged.Count - 2] = new RawPiece(candidateText, previous.HeaderContext, previous.OffsetEligible && last.OffsetEligible);
                        merged.RemoveAt(merged.Count - 1);
                    }
                }
            }

            return merged;
        }

        private Chunk Enrich(
            RawPiece piece,
            int position,
            Guid? parent,
            ChunkingOptions options,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            string? offsetSource,
            bool offsetsAllowed,
            ref int searchFrom)
        {
            string body = piece.Text;
            string contextualized = options.ContextualizeHeaders && !string.IsNullOrEmpty(piece.HeaderContext)
                ? piece.HeaderContext + "\n\n" + body
                : body;
            string finalText = string.IsNullOrEmpty(options.ContextPrefix) ? contextualized : options.ContextPrefix + contextualized;

            Chunk chunk = new Chunk
            {
                ParentGUID = parent,
                Position = position,
                Text = finalText,
                CharacterCount = finalText.Length,
                Strategy = options.Strategy,
                TokenizerModel = profile.TokenizerModel,
                HeaderContext = piece.HeaderContext
            };

            if (options.ComputeTokenCounts)
                chunk.TokenCount = tokenizer.CountTokens(finalText);

            if (offsetsAllowed && piece.OffsetEligible && offsetSource != null)
            {
                int index = offsetSource.IndexOf(body, Math.Min(searchFrom, offsetSource.Length), StringComparison.Ordinal);
                if (index >= 0)
                {
                    chunk.StartOffset = index;
                    chunk.EndOffset = index + body.Length;
                    searchFrom = index + 1;
                }
            }

            if (options.ComputeHashes)
                ComputeHashes(chunk, finalText);

            return chunk;
        }

        private static void CopyRequestMetadata(Chunk chunk, SemanticCellRequest request)
        {
            if (request.Labels != null && request.Labels.Count > 0)
                chunk.Labels = new List<string>(request.Labels);

            if (request.Tags != null && request.Tags.Count > 0)
                chunk.Tags = new Dictionary<string, string>(request.Tags);
        }

        private static void ComputeHashes(Chunk chunk, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (MD5 md5 = MD5.Create()) chunk.MD5Hash = md5.ComputeHash(bytes);
            using (SHA1 sha1 = SHA1.Create()) chunk.SHA1Hash = sha1.ComputeHash(bytes);
            using (SHA256 sha256 = SHA256.Create()) chunk.SHA256Hash = sha256.ComputeHash(bytes);
        }

        private async Task<ResolvedTokenizationProfile> ResolveProfileAsync(ChunkingOptions options, CancellationToken token)
        {
            return await _Resolver.ResolveAsync(
                options.TokenizerKind,
                options.ApiFormat,
                options.ModelId,
                options.EffectiveInputBudget,
                options.AllowCalibration,
                token).ConfigureAwait(false);
        }

        private static int ComputeBudget(ChunkingOptions options, ResolvedTokenizationProfile profile)
        {
            return Math.Max(1, Math.Min(options.MaxTokens, profile.EffectiveInputBudget));
        }

        private static Guid? ResolveParent(SemanticCellRequest request, ChunkingOptions options)
        {
            if (options.ParentGUID != Guid.Empty) return options.ParentGUID;
            if (request.ParentGUID.HasValue && request.ParentGUID.Value != Guid.Empty) return request.ParentGUID.Value;
            return null;
        }

        private static SemanticCellRequest BuildTextRequest(string text, ChunkingOptions options)
        {
            return new SemanticCellRequest
            {
                Type = options.InputType == ContentTypeEnum.List || options.InputType == ContentTypeEnum.Table
                    ? ContentTypeEnum.Text
                    : options.InputType,
                Text = text,
                ParentGUID = options.ParentGUID != Guid.Empty ? options.ParentGUID : null
            };
        }

        private static ChunkingOptions WithInputType(ChunkingOptions options, ContentTypeEnum type)
        {
            if (options.InputType == type) return options;

            ChunkingOptions copy = Clone(options);
            copy.InputType = type;
            return copy;
        }

        private static ChunkingOptions Clone(ChunkingOptions options)
        {
            return new ChunkingOptions
            {
                Strategy = options.Strategy,
                InputType = options.InputType,
                MaxTokens = options.MaxTokens,
                OverlapCount = options.OverlapCount,
                OverlapPercentage = options.OverlapPercentage,
                OverlapCharacters = options.OverlapCharacters,
                OverlapStrategy = options.OverlapStrategy,
                RowGroupSize = options.RowGroupSize,
                RegexPattern = options.RegexPattern,
                ContextPrefix = options.ContextPrefix,
                Separators = options.Separators,
                Format = options.Format,
                ContextualizeHeaders = options.ContextualizeHeaders,
                TokenizerKind = options.TokenizerKind,
                ModelId = options.ModelId,
                ApiFormat = options.ApiFormat,
                EffectiveInputBudget = options.EffectiveInputBudget,
                AllowCalibration = options.AllowCalibration,
                ComputeTokenCounts = options.ComputeTokenCounts,
                ComputeOffsets = options.ComputeOffsets,
                ComputeHashes = options.ComputeHashes,
                HierarchyAware = options.HierarchyAware,
                HeaderContextSeparator = options.HeaderContextSeparator,
                SmallChunkMode = options.SmallChunkMode,
                MinChunkTokens = options.MinChunkTokens,
                TrimWhitespace = options.TrimWhitespace,
                MaxInputCharacters = options.MaxInputCharacters,
                RegexTimeoutMilliseconds = options.RegexTimeoutMilliseconds,
                ParentGUID = options.ParentGUID
            };
        }

        private static void GuardInputSize(int characterCount, ChunkingOptions options)
        {
            if (options.MaxInputCharacters > 0 && characterCount > options.MaxInputCharacters)
                throw new InvalidChunkingOptionsException(
                    "Input length " + characterCount + " characters exceeds MaxInputCharacters " + options.MaxInputCharacters
                    + ". Increase MaxInputCharacters or pre split the input.");
        }

        private static FileShare MapShare(FileAccessModeEnum accessMode)
        {
            switch (accessMode)
            {
                case FileAccessModeEnum.Exclusive:
                    return FileShare.None;
                case FileAccessModeEnum.SharedReadWrite:
                    return FileShare.ReadWrite;
                default:
                    return FileShare.Read;
            }
        }

        private static async Task<List<Chunk>> CollectAsync(IAsyncEnumerable<Chunk> source)
        {
            List<Chunk> list = new List<Chunk>();
            await foreach (Chunk chunk in source.ConfigureAwait(false))
                list.Add(chunk);
            return list;
        }
    }
}
