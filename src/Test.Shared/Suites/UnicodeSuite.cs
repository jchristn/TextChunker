namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies chunking of non Latin scripts, emoji (surrogate pairs), and combining marks, with a focus on
    /// offset correctness and budget adherence.
    /// </summary>
    public static class UnicodeSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            string cjk = string.Concat(Enumerable.Repeat("这是一个用于测试分块的中文句子。", 40));
            string emoji = string.Concat(Enumerable.Repeat("Hello 👋 world 🌍 celebrate 🎉 here. ", 40));
            string combining = string.Concat(Enumerable.Repeat("café naïve résumé Zoë coöperate. ", 40));

            return new TestSuiteDescriptor(
                suiteId: "Unicode",
                displayName: "Unicode and Emoji",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Unicode", "CjkBudgetAndOffsets", "CJK text chunks within budget and offsets index back exactly",
                        executeAsync: ct =>
                        {
                            AssertBudgetAndOffsets(cjk, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 24 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "EmojiSurrogateOffsets", "Emoji surrogate pairs never corrupt offsets",
                        executeAsync: ct =>
                        {
                            AssertBudgetAndOffsets(emoji, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 20 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "CombiningMarks", "Combining marks chunk within budget with exact offsets",
                        executeAsync: ct =>
                        {
                            AssertBudgetAndOffsets(combining, new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 24 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "WordPieceCjk", "WordPiece counts and chunks CJK within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(cjk, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 24, ModelId = "all-minilm" });
                            TestSupport.Assert(chunks.Count > 1, "expected multiple WordPiece chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 24, "WordPiece chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        })
                });
        }

        private static void AssertBudgetAndOffsets(string source, ChunkingOptions options)
        {
            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, options);
            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks for the unicode corpus");
            foreach (Chunk c in chunks)
            {
                TestSupport.Assert(c.TokenCount <= options.MaxTokens, "unicode chunk over budget: " + c.TokenCount);
                if (c.StartOffset >= 0)
                {
                    string sub = source.Substring(c.StartOffset, c.EndOffset - c.StartOffset);
                    TestSupport.Assert(string.Equals(sub, c.Text, StringComparison.Ordinal), "unicode offset substring did not match chunk text");
                }
            }
        }
    }
}
