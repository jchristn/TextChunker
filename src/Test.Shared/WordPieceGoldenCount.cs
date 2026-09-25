namespace Test.Shared
{
    /// <summary>
    /// One golden WordPiece count: an input, the count the local tokenizer must produce, and the count the embedding
    /// runtime (Ollama all-minilm, prompt_eval_count minus [CLS] and [SEP]) charged when the fixture was captured.
    /// </summary>
    public sealed class WordPieceGoldenCount
    {
        /// <summary>Input category, for failure messages.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Input text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Expected local token count.</summary>
        public int Count { get; set; } = 0;

        /// <summary>Token count measured from the runtime.</summary>
        public int RuntimeCount { get; set; } = 0;
    }
}
