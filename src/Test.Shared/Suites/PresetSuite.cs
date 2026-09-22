namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the option presets produce their intended strategy and stay within budget.
    /// </summary>
    public static class PresetSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Preset",
                displayName: "Option Presets",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Preset", "ForRag", "ForRag uses fixed token chunks with overlap within budget",
                        executeAsync: ct =>
                        {
                            ChunkingOptions options = ChunkingOptions.ForRag();
                            TestSupport.Assert(options.Strategy == ChunkStrategyEnum.FixedTokenCount, "ForRag should be fixed token");
                            TestSupport.Assert(options.OverlapCount > 0, "ForRag should set overlap");
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(2000), options);
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= options.MaxTokens, "ForRag chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Preset", "ForSummarization", "ForSummarization uses paragraph chunks within budget",
                        executeAsync: ct =>
                        {
                            ChunkingOptions options = ChunkingOptions.ForSummarization();
                            TestSupport.Assert(options.Strategy == ChunkStrategyEnum.ParagraphBased, "ForSummarization should be paragraph based");
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.MultiParagraph, options);
                            TestSupport.Assert(chunks.Count >= 1, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= options.MaxTokens, "ForSummarization chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Preset", "ForLargeContext", "ForLargeContext uses large fixed token chunks within budget",
                        executeAsync: ct =>
                        {
                            ChunkingOptions options = ChunkingOptions.ForLargeContext();
                            TestSupport.Assert(options.Strategy == ChunkStrategyEnum.FixedTokenCount, "ForLargeContext should be fixed token");
                            TestSupport.Assert(options.MaxTokens >= 2048, "ForLargeContext should use a large budget");
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(6000), options);
                            TestSupport.Assert(chunks.Count >= 1, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= options.MaxTokens, "ForLargeContext chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
