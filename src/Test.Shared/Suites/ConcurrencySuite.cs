namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies that a single chunker instance is safe to use concurrently and stays deterministic.
    /// </summary>
    public static class ConcurrencySuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Concurrency",
                displayName: "Concurrent Use",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Concurrency", "SharedInstanceDeterministic", "One instance chunked from many threads returns identical results",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            string source = TestSupport.WordCorpus(500);
                            ChunkingOptions options = new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 32, OverlapCount = 8 };

                            IReadOnlyList<Chunk> baseline = chunker.Chunk(source, options);

                            List<Task<IReadOnlyList<Chunk>>> tasks = new List<Task<IReadOnlyList<Chunk>>>();
                            for (int i = 0; i < 16; i++)
                                tasks.Add(Task.Run(() => chunker.Chunk(source, options)));

                            IReadOnlyList<Chunk>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

                            foreach (IReadOnlyList<Chunk> result in results)
                            {
                                TestSupport.Assert(result.Count == baseline.Count, "concurrent run produced a different chunk count");
                                for (int i = 0; i < result.Count; i++)
                                    TestSupport.Assert(string.Equals(result[i].Text, baseline[i].Text, StringComparison.Ordinal), "concurrent run diverged at chunk " + i);
                            }
                        })
                });
        }
    }
}
