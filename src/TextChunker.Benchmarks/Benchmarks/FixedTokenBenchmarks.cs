namespace TextChunker.Benchmarks.Benchmarks
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;

    /// <summary>
    /// Measures fixed token chunking throughput across document sizes.
    /// </summary>
    [MemoryDiagnoser]
    public class FixedTokenBenchmarks
    {
        private readonly Chunker _Chunker = new Chunker();
        private string _Small = string.Empty;
        private string _Large = string.Empty;

        /// <summary>
        /// Prepare corpora.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _Small = CorpusFactory.Words(200);
            _Large = CorpusFactory.Words(5000);
        }

        /// <summary>
        /// Chunk a small document.
        /// </summary>
        /// <returns>Chunk count.</returns>
        [Benchmark]
        public int SmallFixed()
        {
            IReadOnlyList<Chunk> chunks = _Chunker.Chunk(_Small, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 64, OverlapCount = 16 });
            return chunks.Count;
        }

        /// <summary>
        /// Chunk a large document.
        /// </summary>
        /// <returns>Chunk count.</returns>
        [Benchmark]
        public int LargeFixed()
        {
            IReadOnlyList<Chunk> chunks = _Chunker.Chunk(_Large, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 256, OverlapCount = 32 });
            return chunks.Count;
        }
    }
}
