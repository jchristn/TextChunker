namespace TextChunker.Tokenization
{
    using TextChunker.Enums;

    /// <summary>
    /// A provider default tokenization entry: which tokenizer family and maximum input budget a provider uses.
    /// </summary>
    public class TokenizationDefaultEntry
    {
        /// <summary>
        /// Tokenizer family for this provider default.
        /// </summary>
        public TokenizerKindEnum TokenizerKind { get; set; } = TokenizerKindEnum.Cl100kBase;

        /// <summary>
        /// Tokenizer model or vocabulary identifier for this provider default.
        /// </summary>
        public string TokenizerModel { get; set; } = "cl100k_base";

        /// <summary>
        /// Maximum accepted input tokens for this provider default.
        /// </summary>
        public int MaxInputTokens { get; set; } = 8192;

        /// <summary>
        /// Initialize a new provider default entry.
        /// </summary>
        /// <param name="tokenizerKind">Tokenizer family.</param>
        /// <param name="tokenizerModel">Tokenizer model or vocabulary identifier.</param>
        /// <param name="maxInputTokens">Maximum accepted input tokens.</param>
        public TokenizationDefaultEntry(TokenizerKindEnum tokenizerKind, string tokenizerModel, int maxInputTokens)
        {
            TokenizerKind = tokenizerKind;
            TokenizerModel = tokenizerModel;
            MaxInputTokens = maxInputTokens;
        }
    }
}
