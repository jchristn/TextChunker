namespace TextChunker.Benchmarks.Benchmarks
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;

    /// <summary>
    /// Exercises the unit packing path on a large document, so a regression toward quadratic behavior is visible.
    /// </summary>
    [MemoryDiagnoser]
    public class LargeDocumentBenchmarks
    {
        private readonly Chunker _Chunker = new Chunker();
        private string _Large = string.Empty;

        /// <summary>
        /// Prepare corpus.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _Large = CorpusFactory.Prose(1000);
        }

        /// <summary>
        /// Chunk a large document by sentence.
        /// </summary>
        /// <returns>Chunk count.</returns>
        [Benchmark]
        public int LargeSentence()
        {
            IReadOnlyList<Chunk> chunks = _Chunker.Chunk(_Large, new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 256 });
            return chunks.Count;
        }
    }
}
