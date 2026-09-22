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
    /// Verifies each strategy produces non empty, in budget chunks for representative input.
    /// </summary>
    public static class StrategySuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Strategy",
                displayName: "Chunking Strategies",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Strategy", "Fixed", "FixedTokenCount produces in budget chunks",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.WordCorpus(200),
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 32 });
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount > 0 && c.TokenCount <= 32, "chunk out of budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "Sentence", "SentenceBased groups sentences within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.MultiParagraph,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 40 });
                            TestSupport.Assert(chunks.Count > 0, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 40, "sentence chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "Paragraph", "ParagraphBased groups paragraphs within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.MultiParagraph,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.ParagraphBased, MaxTokens = 64 });
                            TestSupport.Assert(chunks.Count > 0, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 64, "paragraph chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "Regex", "RegexBased splits on the supplied delimiter",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                "alpha;;beta;;gamma;;delta",
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.RegexBased, RegexPattern = ";;", MaxTokens = 64 });
                            TestSupport.Assert(chunks.Count == 4, "expected 4 segments, got " + chunks.Count);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "ListEntry", "ListEntry produces one chunk per item",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkList(new[] { "first item", "second item", "third item" }, false,
                                    new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64 }, ct));
                            TestSupport.Assert(chunks.Count == 3, "expected 3 list chunks, got " + chunks.Count);
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "TableRowWithHeaders", "Table RowWithHeaders emits a chunk per data row",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Name", "Role" },
                                new List<string> { "Ada", "Engineer" },
                                new List<string> { "Bob", "Designer" }
                            };
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.RowWithHeaders, MaxTokens = 128 }, ct));
                            TestSupport.Assert(chunks.Count == 2, "expected 2 row chunks, got " + chunks.Count);
                            TestSupport.Assert(chunks[0].Text.Contains("Name"), "row chunk should include headers");
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "TablePipeEscape", "Table serialization escapes pipe characters in cells",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Name", "Note" },
                                new List<string> { "Ada", "a|b|c" }
                            };
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.WholeTable, MaxTokens = 128 }, ct));
                            TestSupport.Assert(chunks.Count == 1, "expected 1 whole table chunk");
                            TestSupport.Assert(chunks[0].Text.Contains("a\\|b\\|c"), "pipe characters in cells should be escaped");
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "EmptyInput", "Empty input yields no chunks",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(string.Empty, new ChunkingOptions());
                            TestSupport.Assert(chunks.Count == 0, "empty input should yield no chunks");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "WholeList", "WholeList emits a single chunk for a small list",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkList(new[] { "one", "two", "three" }, true,
                                    new ChunkingOptions { Strategy = ChunkStrategyEnum.WholeList, MaxTokens = 128 }, ct));
                            TestSupport.Assert(chunks.Count == 1, "expected one whole-list chunk, got " + chunks.Count);
                            TestSupport.Assert(chunks[0].Text.Contains("1. one"), "ordered list should be numbered");
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "TableRow", "Table Row emits space-joined values per data row",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = Table();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.Row, MaxTokens = 128 }, ct));
                            TestSupport.Assert(chunks.Count == 2, "expected 2 row chunks, got " + chunks.Count);
                            TestSupport.Assert(!chunks[0].Text.Contains("Name"), "Row strategy should not include headers");
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "TableRowGroupWithHeaders", "Table RowGroupWithHeaders groups rows with headers",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = Table();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.RowGroupWithHeaders, RowGroupSize = 2, MaxTokens = 256 }, ct));
                            TestSupport.Assert(chunks.Count == 1, "expected one group chunk for two rows, got " + chunks.Count);
                            TestSupport.Assert(chunks[0].Text.Contains("Name") && chunks[0].Text.Contains("Ada") && chunks[0].Text.Contains("Bob"), "group should contain headers and both rows");
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Strategy", "TableKeyValuePairs", "Table KeyValuePairs emits header: value pairs per row",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = Table();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.KeyValuePairs, MaxTokens = 128 }, ct));
                            TestSupport.Assert(chunks.Count == 2, "expected 2 key-value chunks, got " + chunks.Count);
                            TestSupport.Assert(chunks[0].Text.Contains("Name: Ada") && chunks[0].Text.Contains("Role: Engineer"), "expected key: value formatting");
                            await Task.CompletedTask;
                        })
                });
        }

        private static List<IReadOnlyList<string>> Table()
        {
            return new List<IReadOnlyList<string>>
            {
                new List<string> { "Name", "Role" },
                new List<string> { "Ada", "Engineer" },
                new List<string> { "Bob", "Designer" }
            };
        }
    }
}
