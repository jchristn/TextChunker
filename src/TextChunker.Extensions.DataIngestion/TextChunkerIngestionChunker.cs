namespace TextChunker.Extensions.DataIngestion
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using global::Microsoft.Extensions.DataIngestion;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;

    /// <summary>
    /// A Microsoft.Extensions.DataIngestion chunker backed by TextChunker. Reconstructs the document's markdown,
    /// chunks it with TextChunker, and emits one IngestionChunk per produced chunk with TextChunker's metadata
    /// (position, token count, offsets, strategy, tokenizer, header context) attached. Drop it into an
    /// IngestionPipeline as the chunking stage.
    /// </summary>
    public sealed class TextChunkerIngestionChunker : IngestionChunker<string>
    {
        private readonly IChunker _Chunker;
        private readonly ChunkingOptions _Options;

        /// <summary>
        /// Initialize a chunker with default options and a default TextChunker.
        /// </summary>
        public TextChunkerIngestionChunker()
            : this(null, null)
        {
        }

        /// <summary>
        /// Initialize a chunker with the supplied options and a default TextChunker.
        /// </summary>
        /// <param name="options">Chunking options, or null for defaults.</param>
        public TextChunkerIngestionChunker(ChunkingOptions? options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Initialize a chunker with the supplied options and chunker.
        /// </summary>
        /// <param name="options">Chunking options, or null for defaults.</param>
        /// <param name="chunker">The chunker to use, or null to construct a default one.</param>
        public TextChunkerIngestionChunker(ChunkingOptions? options, IChunker? chunker)
        {
            _Options = options ?? new ChunkingOptions();
            _Chunker = chunker ?? new Chunker();
        }

        /// <summary>
        /// Split a document into chunks.
        /// </summary>
        /// <param name="document">The document to chunk.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An asynchronous stream of ingestion chunks.</returns>
        /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
        public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
            IngestionDocument document,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            string markdown = IngestionDocumentMarkdown.Build(document);
            SemanticCellRequest request = new SemanticCellRequest
            {
                Type = ContentTypeEnum.Text,
                Text = markdown
            };

            await foreach (Chunk chunk in _Chunker.ChunkRequest(request, _Options, cancellationToken).ConfigureAwait(false))
            {
                IngestionChunk<string> result = new IngestionChunk<string>(
                    chunk.Text,
                    document,
                    chunk.HeaderContext ?? string.Empty);

                result.Metadata["textchunker.position"] = chunk.Position;
                result.Metadata["textchunker.tokenCount"] = chunk.TokenCount;
                result.Metadata["textchunker.characterCount"] = chunk.CharacterCount;
                result.Metadata["textchunker.startOffset"] = chunk.StartOffset;
                result.Metadata["textchunker.endOffset"] = chunk.EndOffset;
                result.Metadata["textchunker.strategy"] = chunk.Strategy.ToString();
                result.Metadata["textchunker.tokenizer"] = chunk.TokenizerModel;

                yield return result;
            }
        }
    }
}
