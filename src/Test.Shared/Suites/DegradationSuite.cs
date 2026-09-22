namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Exercises the graceful degradation paths: oversized tables, lists, and structured inputs driven with a
    /// text strategy, all of which fall back to finer splitting while staying within budget.
    /// </summary>
    public static class DegradationSuite
    {
        private static string LongText(int words)
        {
            return string.Join(" ", Enumerable.Range(1, words).Select(i => "detail" + i));
        }

        private static IReadOnlyList<Chunk> Collect(System.Collections.Generic.IAsyncEnumerable<Chunk> stream)
        {
            return TestSupport.Collect(stream);
        }

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Degradation",
                displayName: "Graceful Degradation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Degradation", "OversizedTableRowGroup", "An oversized table group degrades to rows then cells within budget",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Name", "Bio" },
                                new List<string> { "Ada", LongText(40) },
                                new List<string> { "Bob", LongText(40) }
                            };
                            IReadOnlyList<Chunk> chunks = Collect(chunker.ChunkTable(rows,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.RowGroupWithHeaders, RowGroupSize = 2, MaxTokens = 16 }));
                            TestSupport.Assert(chunks.Count > 2, "expected degradation to produce several chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 16, "degraded table chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Degradation", "OversizedKeyValuePairs", "Oversized key value rows degrade within budget",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Field", "Detail" },
                                new List<string> { "Summary", LongText(50) }
                            };
                            IReadOnlyList<Chunk> chunks = Collect(chunker.ChunkTable(rows,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.KeyValuePairs, MaxTokens = 16 }));
                            TestSupport.Assert(chunks.Count > 1, "expected key value degradation");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 16, "degraded key value chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Degradation", "OversizedWholeList", "An oversized whole list splits by line within budget",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<string> items = Enumerable.Range(1, 30).Select(i => "Step " + i + " describes an action with several descriptive words attached here.").ToList();
                            IReadOnlyList<Chunk> chunks = Collect(chunker.ChunkList(items, true,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.WholeList, MaxTokens = 24 }));
                            TestSupport.Assert(chunks.Count > 1, "oversized whole list should split");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 24, "whole list chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Degradation", "ListWithTextStrategy", "A list chunked with a text strategy serializes and chunks",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<string> items = new List<string> { "alpha", "beta", "gamma", "delta", "epsilon" };
                            IReadOnlyList<Chunk> chunks = Collect(chunker.ChunkList(items, true,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 16 }));
                            TestSupport.Assert(chunks.Count >= 1, "expected chunks from serialized list");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 16, "serialized list chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Degradation", "TableWithTextStrategy", "A table chunked with a text strategy serializes and chunks",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Name", "Role" },
                                new List<string> { "Ada", "Engineer" },
                                new List<string> { "Bob", "Designer" }
                            };
                            IReadOnlyList<Chunk> chunks = Collect(chunker.ChunkTable(rows,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 32 }));
                            TestSupport.Assert(chunks.Count >= 1, "expected chunks from serialized table");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 32, "serialized table chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
