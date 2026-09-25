namespace TextChunker.Tokenization
{
    using System;

    /// <summary>
    /// Normalization and pre-tokenization settings for the BERT WordPiece tokenizer. The defaults reproduce the
    /// Hugging Face <c>BertTokenizer</c> configuration for an uncased vocabulary, which is also what embedding
    /// runtimes such as Ollama, llama.cpp, and sentence-transformers apply, so local token counts match the count the
    /// runtime enforces.
    /// </summary>
    public class WordPieceOptions
    {
        private int _MaxInputCharactersPerWord = 0;
        private string _UnknownToken = "[UNK]";

        /// <summary>
        /// When true, text is lower cased before WordPiece lookup. Required for uncased vocabularies such as
        /// bert-base-uncased. Default true.
        /// </summary>
        public bool LowerCase { get; set; } = true;

        /// <summary>
        /// When true, text is decomposed (NFD) and nonspacing marks are removed, so accented characters match an
        /// uncased vocabulary that was built without accents. Hugging Face enables this whenever lower casing is on
        /// and the runtime does the same. Default true.
        /// </summary>
        public bool StripAccents { get; set; } = true;

        /// <summary>
        /// When true, each CJK ideograph is treated as its own word, matching the BERT basic tokenizer. Default true.
        /// </summary>
        public bool TokenizeCjkCharacters { get; set; } = true;

        /// <summary>
        /// Words longer than this many code points become a single unknown token. Hugging Face uses 100, but
        /// llama.cpp based runtimes such as Ollama apply no limit and segment the whole word, so a long URL or encoded
        /// blob costs far more than one token there. Default 0, which disables the limit and always segments, keeping
        /// the count at or above what either runtime charges. Minimum 0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0.</exception>
        public int MaxInputCharactersPerWord
        {
            get => _MaxInputCharactersPerWord;
            set => _MaxInputCharactersPerWord = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxInputCharactersPerWord), "MaxInputCharactersPerWord must be at least 0.");
        }

        /// <summary>
        /// The vocabulary entry emitted for a word that cannot be segmented. Default "[UNK]". Must be present in the
        /// vocabulary.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or whitespace.</exception>
        public string UnknownToken
        {
            get => _UnknownToken;
            set => _UnknownToken = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentNullException(nameof(UnknownToken))
                : value;
        }

        /// <summary>
        /// Create a copy of these options.
        /// </summary>
        /// <returns>A new options instance with the same values.</returns>
        public WordPieceOptions Clone()
        {
            return new WordPieceOptions
            {
                LowerCase = LowerCase,
                StripAccents = StripAccents,
                TokenizeCjkCharacters = TokenizeCjkCharacters,
                MaxInputCharactersPerWord = MaxInputCharactersPerWord,
                UnknownToken = UnknownToken
            };
        }
    }
}
