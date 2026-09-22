namespace TextChunker.Benchmarks.Benchmarks
{
    using BenchmarkDotNet.Attributes;
    using TextChunker.Tokenization;

    /// <summary>
    /// Compares token counting for the two tokenizer families.
    /// </summary>
    [MemoryDiagnoser]
    public class TokenizerBenchmarks
    {
        private readonly ITokenizerAdapter _Cl100k = new SharpTokenTokenizerAdapter("cl100k_base");
        private readonly ITokenizerAdapter _WordPiece = new BertWordPieceTokenizerAdapter();
        private string _Text = string.Empty;

        /// <summary>
        /// Prepare corpus.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _Text = CorpusFactory.Prose(20);
        }

        /// <summary>
        /// Count with cl100k.
        /// </summary>
        /// <returns>Token count.</returns>
        [Benchmark]
        public int Cl100kCount()
        {
            return _Cl100k.CountTokens(_Text);
        }

        /// <summary>
        /// Count with WordPiece.
        /// </summary>
        /// <returns>Token count.</returns>
        [Benchmark]
        public int WordPieceCount()
        {
            return _WordPiece.CountTokens(_Text);
        }
    }
}
