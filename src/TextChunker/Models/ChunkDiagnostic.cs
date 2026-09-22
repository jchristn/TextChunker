namespace TextChunker.Models
{
    using TextChunker.Enums;

    /// <summary>
    /// Run level diagnostics describing how a chunking operation resolved its tokenizer and budget and what it
    /// produced. Returned by the materializing entry point that computes a full result.
    /// </summary>
    public class ChunkDiagnostic
    {
        /// <summary>
        /// The tokenizer family used for the operation.
        /// </summary>
        public TokenizerKindEnum TokenizerKind { get; set; } = TokenizerKindEnum.Cl100kBase;

        /// <summary>
        /// The tokenizer model or vocabulary identifier used for the operation.
        /// </summary>
        public string TokenizerModel { get; set; } = string.Empty;

        /// <summary>
        /// Where the tokenization profile was resolved from.
        /// </summary>
        public TokenizationProfileSourceEnum ProfileSource { get; set; } = TokenizationProfileSourceEnum.GlobalFallback;

        /// <summary>
        /// The effective token budget enforced while chunking, after context prefix accounting.
        /// </summary>
        public int EffectiveTokenBudget { get; set; } = 0;

        /// <summary>
        /// The strategy used for the operation.
        /// </summary>
        public ChunkStrategyEnum Strategy { get; set; } = ChunkStrategyEnum.FixedTokenCount;

        /// <summary>
        /// Number of chunks produced.
        /// </summary>
        public int ChunkCount { get; set; } = 0;

        /// <summary>
        /// Total token count across all produced chunks.
        /// </summary>
        public int TotalTokens { get; set; } = 0;

        /// <summary>
        /// Largest chunk token count produced.
        /// </summary>
        public int MaxChunkTokens { get; set; } = 0;

        /// <summary>
        /// Number of source characters read.
        /// </summary>
        public int InputCharacterCount { get; set; } = 0;
    }
}
