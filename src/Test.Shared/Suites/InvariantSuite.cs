namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Property style checks over generated and pathological inputs across a sweep of strategies and options.
    /// </summary>
    public static class InvariantSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Invariant",
                displayName: "Chunking Invariants",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Invariant", "NoChunkExceedsBudget", "No chunk exceeds the budget across a strategy and option sweep",
                        executeAsync: ct =>
                        {
                            string[] corpora = new[]
                            {
                                TestSupport.WordCorpus(300),
                                TestSupport.MultiParagraph,
                                "Single. Sentence. Fragments. Everywhere. Now.",
                                "one\n\ntwo\n\nthree\n\nfour"
                            };
                            ChunkStrategyEnum[] strategies = new[]
                            {
                                ChunkStrategyEnum.FixedTokenCount,
                                ChunkStrategyEnum.SentenceBased,
                                ChunkStrategyEnum.ParagraphBased
                            };
                            int[] budgets = new[] { 8, 16, 32, 64 };

                            foreach (string corpus in corpora)
                                foreach (ChunkStrategyEnum strategy in strategies)
                                    foreach (int budget in budgets)
                                    {
                                        IReadOnlyList<Chunk> chunks = TestSupport.Chunk(corpus, new ChunkingOptions { Strategy = strategy, MaxTokens = budget });
                                        foreach (Chunk c in chunks)
                                            TestSupport.Assert(c.TokenCount <= budget, "chunk exceeded budget " + budget + " for " + strategy + ": " + c.TokenCount);
                                    }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Invariant", "NonOverlappingReconstructsSource", "Non overlapping fixed token chunks reconstruct all source words in order",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.WordCorpus(150);
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { MaxTokens = 24, OverlapCount = 0, TrimWhitespace = true });

                            System.Text.StringBuilder rebuilt = new System.Text.StringBuilder();
                            foreach (Chunk c in chunks)
                            {
                                if (rebuilt.Length > 0) rebuilt.Append(' ');
                                rebuilt.Append(c.Text.Trim());
                            }

                            string[] expected = source.Split(' ');
                            string[] actual = rebuilt.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            TestSupport.Assert(expected.Length == actual.Length, "word count changed: " + expected.Length + " vs " + actual.Length);
                            for (int i = 0; i < expected.Length; i++)
                                TestSupport.Assert(string.Equals(expected[i], actual[i], StringComparison.Ordinal), "word mismatch at " + i);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Invariant", "PathologicalInputsTerminate", "Pathological inputs terminate and never exceed budget",
                        executeAsync: ct =>
                        {
                            string[] inputs = new[]
                            {
                                string.Empty,
                                "   ",
                                "\n\n\n\n",
                                new string('a', 5000),
                                "word " + new string('x', 4000) + " word"
                            };
                            foreach (string input in inputs)
                            {
                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(input, new ChunkingOptions { MaxTokens = 16 });
                                foreach (Chunk c in chunks)
                                    TestSupport.Assert(c.TokenCount <= 16, "pathological chunk exceeded budget: " + c.TokenCount);
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Invariant", "RecursiveAndRegexInBudget", "Recursive and regex strategies stay within budget with exact offsets",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.MultiParagraph;
                            int[] budgets = new[] { 12, 24, 48 };
                            foreach (int budget in budgets)
                            {
                                IReadOnlyList<Chunk> recursive = TestSupport.Chunk(source, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = budget });
                                foreach (Chunk c in recursive)
                                {
                                    TestSupport.Assert(c.TokenCount <= budget, "recursive chunk over budget " + budget + ": " + c.TokenCount);
                                    if (c.StartOffset >= 0)
                                    {
                                        string sub = source.Substring(c.StartOffset, c.EndOffset - c.StartOffset);
                                        TestSupport.Assert(string.Equals(sub, c.Text, System.StringComparison.Ordinal), "recursive offset substring mismatch");
                                    }
                                }

                                IReadOnlyList<Chunk> regex = TestSupport.Chunk(source, new ChunkingOptions { Strategy = ChunkStrategyEnum.RegexBased, RegexPattern = "\\s+", MaxTokens = budget });
                                foreach (Chunk c in regex)
                                    TestSupport.Assert(c.TokenCount <= budget, "regex chunk over budget " + budget + ": " + c.TokenCount);
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Invariant", "LargeDocument", "A large document chunks within budget with contiguous ordinals",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.WordCorpus(20000);
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 256, OverlapCount = 0 });
                            TestSupport.Assert(chunks.Count > 50, "expected many chunks from a large document, got " + chunks.Count);
                            for (int i = 0; i < chunks.Count; i++)
                            {
                                TestSupport.Assert(chunks[i].Position == i, "large document ordinal mismatch at " + i);
                                TestSupport.Assert(chunks[i].TokenCount <= 256, "large document chunk over budget: " + chunks[i].TokenCount);
                            }
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
