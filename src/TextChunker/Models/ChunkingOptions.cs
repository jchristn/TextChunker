namespace TextChunker.Models
{
    using System;
    using TextChunker.Enums;
    using TextChunker.Exceptions;

    /// <summary>
    /// Parameters that control a chunking operation. Passing null to a chunking method uses a default instance.
    /// Properties that carry a range or nullability constraint validate on assignment and throw
    /// <see cref="InvalidChunkingOptionsException"/> on violation.
    /// </summary>
    public class ChunkingOptions
    {
        private int _MaxTokens = 512;
        private int _OverlapCount = 0;
        private double? _OverlapPercentage = null;
        private int? _OverlapCharacters = null;
        private int _RowGroupSize = 5;
        private int _MinChunkTokens = 0;
        private int _MaxInputCharacters = 4_000_000;
        private int _RegexTimeoutMilliseconds = 5000;
        private string _HeaderContextSeparator = " > ";

        /// <summary>
        /// Chunking strategy. Default FixedTokenCount.
        /// </summary>
        public ChunkStrategyEnum Strategy { get; set; } = ChunkStrategyEnum.FixedTokenCount;

        /// <summary>
        /// The content type of the input, which drives strategy routing. Default Text. The structured entry
        /// points set this automatically.
        /// </summary>
        public ContentTypeEnum InputType { get; set; } = ContentTypeEnum.Text;

        /// <summary>
        /// Desired chunk size in tokens. Default 512. Minimum 1.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 1.</exception>
        public int MaxTokens
        {
            get => _MaxTokens;
            set => _MaxTokens = value >= 1
                ? value
                : throw new InvalidChunkingOptionsException("MaxTokens must be at least 1.");
        }

        /// <summary>
        /// Overlap between chunks measured in tokens for token strategies and in whole units for unit strategies.
        /// Default 0. Minimum 0.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 0.</exception>
        public int OverlapCount
        {
            get => _OverlapCount;
            set => _OverlapCount = value >= 0
                ? value
                : throw new InvalidChunkingOptionsException("OverlapCount must be at least 0.");
        }

        /// <summary>
        /// Alternative overlap as a fraction of chunk size between 0.0 and 1.0. Takes precedence over
        /// OverlapCharacters and OverlapCount when set.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set outside the range 0.0 to 1.0.</exception>
        public double? OverlapPercentage
        {
            get => _OverlapPercentage;
            set
            {
                if (value.HasValue && (value.Value < 0.0 || value.Value > 1.0))
                    throw new InvalidChunkingOptionsException("OverlapPercentage must be between 0.0 and 1.0.");
                _OverlapPercentage = value;
            }
        }

        /// <summary>
        /// Alternative overlap expressed in characters, converted to an approximate token overlap using the
        /// text's average token density. Takes precedence over OverlapCount, but OverlapPercentage takes
        /// precedence over this. Minimum 0.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 0.</exception>
        public int? OverlapCharacters
        {
            get => _OverlapCharacters;
            set
            {
                if (value.HasValue && value.Value < 0)
                    throw new InvalidChunkingOptionsException("OverlapCharacters must be at least 0.");
                _OverlapCharacters = value;
            }
        }

        /// <summary>
        /// Boundary handling for overlap. Default SlidingWindow.
        /// </summary>
        public OverlapStrategyEnum OverlapStrategy { get; set; } = OverlapStrategyEnum.SlidingWindow;

        /// <summary>
        /// Number of rows per group for the RowGroupWithHeaders strategy. Default 5. Minimum 1.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 1.</exception>
        public int RowGroupSize
        {
            get => _RowGroupSize;
            set => _RowGroupSize = value >= 1
                ? value
                : throw new InvalidChunkingOptionsException("RowGroupSize must be at least 1.");
        }

        /// <summary>
        /// Split delimiter regular expression, required when using the RegexBased strategy.
        /// </summary>
        public string? RegexPattern { get; set; } = null;

        /// <summary>
        /// Text prefix applied to each chunk. Its token cost is measured once and subtracted from the working
        /// budget so a prefixed chunk still fits the model input limit.
        /// </summary>
        public string? ContextPrefix { get; set; } = null;

        /// <summary>
        /// Requested tokenizer family. Default Auto, which lets the resolver choose from the model or provider hint.
        /// </summary>
        public TokenizerKindEnum TokenizerKind { get; set; } = TokenizerKindEnum.Auto;

        /// <summary>
        /// Model identifier used to resolve the tokenizer family and token budget.
        /// </summary>
        public string? ModelId { get; set; } = null;

        /// <summary>
        /// Upstream provider API format hint used to resolve provider default budgets. Default Unknown.
        /// </summary>
        public ApiFormatEnum ApiFormat { get; set; } = ApiFormatEnum.Unknown;

        /// <summary>
        /// Hard override of the resolved effective input budget in tokens, or null to use the resolved value.
        /// </summary>
        public int? EffectiveInputBudget { get; set; } = null;

        /// <summary>
        /// When true and a calibration probe is configured, the resolver may calibrate the budget against a live
        /// endpoint. Default true.
        /// </summary>
        public bool AllowCalibration { get; set; } = true;

        /// <summary>
        /// When true, each chunk's token count is computed. Default true.
        /// </summary>
        public bool ComputeTokenCounts { get; set; } = true;

        /// <summary>
        /// When true, each chunk's source character offsets are computed where the chunk is a literal substring.
        /// Default true.
        /// </summary>
        public bool ComputeOffsets { get; set; } = true;

        /// <summary>
        /// When true, each chunk's MD5, SHA1, and SHA256 hashes are computed. Default false.
        /// </summary>
        public bool ComputeHashes { get; set; } = false;

        /// <summary>
        /// Separator ladder for the Recursive strategy, tried from first to last. When null, the ladder is chosen
        /// from Format. When set, it overrides Format.
        /// </summary>
        public System.Collections.Generic.List<string>? Separators { get; set; } = null;

        /// <summary>
        /// Content format hint for the Recursive strategy. Selects a structure aware separator ladder. Default Plain.
        /// </summary>
        public ContentFormatEnum Format { get; set; } = ContentFormatEnum.Plain;

        /// <summary>
        /// When true and hierarchy aware chunking is on, the breadcrumb header context is prepended to each chunk's
        /// text (not only stored in HeaderContext), and its token cost is charged against the budget. Default false.
        /// </summary>
        public bool ContextualizeHeaders { get; set; } = false;

        /// <summary>
        /// When true, text is chunked section by section using a markdown header hierarchy, and each chunk is
        /// stamped with a breadcrumb header context. Default false.
        /// </summary>
        public bool HierarchyAware { get; set; } = false;

        /// <summary>
        /// Separator used to join breadcrumb segments for hierarchy aware chunking. Default " > ". Never null.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set to null.</exception>
        public string HeaderContextSeparator
        {
            get => _HeaderContextSeparator;
            set => _HeaderContextSeparator = value ?? throw new InvalidChunkingOptionsException("HeaderContextSeparator cannot be null.");
        }

        /// <summary>
        /// How chunks smaller than MinChunkTokens are handled. Default Keep.
        /// </summary>
        public SmallChunkModeEnum SmallChunkMode { get; set; } = SmallChunkModeEnum.Keep;

        /// <summary>
        /// Threshold in tokens for the small chunk mode. Default 0, which disables small chunk handling. Minimum 0.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 0.</exception>
        public int MinChunkTokens
        {
            get => _MinChunkTokens;
            set => _MinChunkTokens = value >= 0
                ? value
                : throw new InvalidChunkingOptionsException("MinChunkTokens must be at least 0.");
        }

        /// <summary>
        /// When true, leading and trailing whitespace is trimmed from each chunk. Default true.
        /// </summary>
        public bool TrimWhitespace { get; set; } = true;

        /// <summary>
        /// Maximum accepted input length in characters. Default 4,000,000. Set to 0 to disable the guard. Minimum 0.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 0.</exception>
        public int MaxInputCharacters
        {
            get => _MaxInputCharacters;
            set => _MaxInputCharacters = value >= 0
                ? value
                : throw new InvalidChunkingOptionsException("MaxInputCharacters must be at least 0.");
        }

        /// <summary>
        /// Timeout in milliseconds for the RegexBased strategy, guarding against catastrophic backtracking.
        /// Default 5000. Minimum 1.
        /// </summary>
        /// <exception cref="InvalidChunkingOptionsException">Thrown when set below 1.</exception>
        public int RegexTimeoutMilliseconds
        {
            get => _RegexTimeoutMilliseconds;
            set => _RegexTimeoutMilliseconds = value >= 1
                ? value
                : throw new InvalidChunkingOptionsException("RegexTimeoutMilliseconds must be at least 1.");
        }

        /// <summary>
        /// Parent identifier stamped onto every produced chunk when set. Default empty, meaning no parent is
        /// stamped and produced chunks carry a null ParentGUID unless the request supplies one.
        /// </summary>
        public Guid ParentGUID { get; set; } = Guid.Empty;

        /// <summary>
        /// Preset tuned for retrieval augmented generation: small overlapping fixed token chunks.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static ChunkingOptions ForRag()
        {
            return new ChunkingOptions
            {
                Strategy = ChunkStrategyEnum.FixedTokenCount,
                MaxTokens = 512,
                OverlapCount = 64
            };
        }

        /// <summary>
        /// Preset tuned for summarization: larger paragraph chunks with no overlap.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static ChunkingOptions ForSummarization()
        {
            return new ChunkingOptions
            {
                Strategy = ChunkStrategyEnum.ParagraphBased,
                MaxTokens = 1024,
                OverlapCount = 0
            };
        }

        /// <summary>
        /// Preset tuned for large context windows: large fixed token chunks with no overlap.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static ChunkingOptions ForLargeContext()
        {
            return new ChunkingOptions
            {
                Strategy = ChunkStrategyEnum.FixedTokenCount,
                MaxTokens = 2048,
                OverlapCount = 0
            };
        }
    }
}
