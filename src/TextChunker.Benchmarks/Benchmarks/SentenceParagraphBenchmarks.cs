namespace TextChunker.Benchmarks.Benchmarks
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;

    /// <summary>
    /// Measures sentence and paragraph chunking throughput.
    /// </summary>
    [MemoryDiagnoser]
    public class SentenceParagraphBenchmarks
    {
        private readonly Chunker _Chunker = new Chunker();
        private string _Prose = string.Empty;

        /// <summary>
        /// Prepare corpora.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _Prose = CorpusFactory.Prose(200);
        }

        /// <summary>
        /// Chunk by sentence.
        /// </summary>
        /// <returns>Chunk count.</returns>
        [Benchmark]
        public int Sentence()
        {
            IReadOnlyList<Chunk> chunks = _Chunker.Chunk(_Prose, new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 128 });
            return chunks.Count;
        }

        /// <summary>
        /// Chunk by paragraph.
        /// </summary>
        /// <returns>Chunk count.</returns>
        [Benchmark]
        public int Paragraph()
        {
            IReadOnlyList<Chunk> chunks = _Chunker.Chunk(_Prose, new ChunkingOptions { Strategy = ChunkStrategyEnum.ParagraphBased, MaxTokens = 128 });
            return chunks.Count;
        }
    }
}
