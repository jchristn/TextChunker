namespace Test.Shared
{
    using System.Collections.Generic;

    /// <summary>
    /// One golden parity case: an input corpus, its chunking parameters, and the expected chunk boundaries.
    /// </summary>
    public sealed class ParityGoldenCase
    {
        /// <summary>Corpus identifier.</summary>
        public string CorpusId { get; set; } = string.Empty;

        /// <summary>Input text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Strategy name.</summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>Maximum tokens per chunk.</summary>
        public int MaxTokens { get; set; } = 0;

        /// <summary>Overlap token count.</summary>
        public int Overlap { get; set; } = 0;

        /// <summary>Expected chunk boundaries.</summary>
        public List<string> Chunks { get; set; } = new List<string>();
    }
}
