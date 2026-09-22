namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies overlap behavior across token count, percentage, and boundary aware strategies.
    /// </summary>
    public static class OverlapSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Overlap",
                displayName: "Overlap Handling",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Overlap", "TokenOverlapProducesMoreChunks", "Token overlap yields at least as many chunks as no overlap",
                        executeAsync: ct =>
                        {
                            string corpus = TestSupport.WordCorpus(200);
                            IReadOnlyList<Chunk> none = TestSupport.Chunk(corpus, new ChunkingOptions { MaxTokens = 32, OverlapCount = 0 });
                            IReadOnlyList<Chunk> overlapped = TestSupport.Chunk(corpus, new ChunkingOptions { MaxTokens = 32, OverlapCount = 8 });
                            TestSupport.Assert(overlapped.Count >= none.Count, "overlap should not reduce chunk count");
                            TestSupport.Assert(overlapped.Count > 1, "expected multiple chunks");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "PercentageOverlapRespectsBudget", "Percentage overlap keeps chunks within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.WordCorpus(200),
                                new ChunkingOptions { MaxTokens = 32, OverlapPercentage = 0.25 });
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 32, "overlap chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "SentenceBoundaryAware", "Sentence boundary aware overlap stays within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.MultiParagraph,
                                new ChunkingOptions { MaxTokens = 40, OverlapCount = 10, OverlapStrategy = OverlapStrategyEnum.SentenceBoundaryAware });
                            TestSupport.Assert(chunks.Count > 0, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 40, "boundary chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "CharacterOverlapRespectsBudget", "Character based overlap produces overlap and stays within budget",
                        executeAsync: ct =>
                        {
                            string corpus = TestSupport.WordCorpus(200);
                            IReadOnlyList<Chunk> none = TestSupport.Chunk(corpus, new ChunkingOptions { MaxTokens = 32, OverlapCount = 0 });
                            IReadOnlyList<Chunk> chars = TestSupport.Chunk(corpus, new ChunkingOptions { MaxTokens = 32, OverlapCharacters = 40 });
                            TestSupport.Assert(chars.Count >= none.Count, "character overlap should not reduce chunk count");
                            foreach (Chunk c in chars)
                                TestSupport.Assert(c.TokenCount <= 32, "character overlap chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "SemanticBoundaryAware", "Semantic boundary aware overlap stays within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.MultiParagraph,
                                new ChunkingOptions { MaxTokens = 48, OverlapCount = 12, OverlapStrategy = OverlapStrategyEnum.SemanticBoundaryAware });
                            TestSupport.Assert(chunks.Count > 0, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 48, "semantic boundary chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "OverlapAtLeastChunkSizeTerminates", "Overlap at or above chunk size still terminates and stays in budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.WordCorpus(80),
                                new ChunkingOptions { MaxTokens = 16, OverlapCount = 16 });
                            TestSupport.Assert(chunks.Count > 0, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 16, "chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "SentenceAwareChangesBoundaries", "Sentence boundary aware overlap actually snaps boundaries",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> sliding = TestSupport.Chunk(TestSupport.MultiParagraph,
                                new ChunkingOptions { MaxTokens = 24, OverlapCount = 8, OverlapStrategy = OverlapStrategyEnum.SlidingWindow });
                            IReadOnlyList<Chunk> aware = TestSupport.Chunk(TestSupport.MultiParagraph,
                                new ChunkingOptions { MaxTokens = 24, OverlapCount = 8, OverlapStrategy = OverlapStrategyEnum.SentenceBoundaryAware });
                            TestSupport.Assert(!Identical(sliding, aware), "sentence boundary aware overlap should differ from sliding window");
                            foreach (Chunk c in aware)
                                TestSupport.Assert(c.TokenCount <= 24, "aware chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Overlap", "SemanticAwareChangesBoundaries", "Semantic boundary aware overlap actually snaps boundaries",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> sliding = TestSupport.Chunk(TestSupport.MultiParagraph,
                                new ChunkingOptions { MaxTokens = 40, OverlapCount = 12, OverlapStrategy = OverlapStrategyEnum.SlidingWindow });
                            IReadOnlyList<Chunk> aware = TestSupport.Chunk(TestSupport.MultiParagraph,
                                new ChunkingOptions { MaxTokens = 40, OverlapCount = 12, OverlapStrategy = OverlapStrategyEnum.SemanticBoundaryAware });
                            TestSupport.Assert(!Identical(sliding, aware), "semantic boundary aware overlap should differ from sliding window");
                            foreach (Chunk c in aware)
                                TestSupport.Assert(c.TokenCount <= 40, "aware chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        })
                });
        }

        private static bool Identical(IReadOnlyList<Chunk> a, IReadOnlyList<Chunk> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i].Text, b[i].Text, System.StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
