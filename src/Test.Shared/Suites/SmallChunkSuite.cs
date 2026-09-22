namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies small chunk handling: keep, merge forward, drop, and the backward merge of a small final chunk.
    /// </summary>
    public static class SmallChunkSuite
    {
        private static IReadOnlyList<Chunk> ChunkList(string[] items, ChunkingOptions options)
        {
            Chunker chunker = new Chunker();
            return TestSupport.Collect(chunker.ChunkList(items, false, options, System.Threading.CancellationToken.None));
        }

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            string[] items = new[] { "a", "b", "c", "this is a considerably longer item that carries real content" };

            return new TestSuiteDescriptor(
                suiteId: "SmallChunk",
                displayName: "Small Chunk Handling",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("SmallChunk", "KeepRetainsAll", "Keep mode retains every chunk including tiny ones",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = ChunkList(items,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64, MinChunkTokens = 3, SmallChunkMode = SmallChunkModeEnum.Keep });
                            TestSupport.Assert(chunks.Count == 4, "keep should retain all four items, got " + chunks.Count);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SmallChunk", "DropRemovesSmall", "Drop mode removes chunks below the token minimum",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = ChunkList(items,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64, MinChunkTokens = 3, SmallChunkMode = SmallChunkModeEnum.Drop });
                            TestSupport.Assert(chunks.Count == 1, "drop should leave only the long item, got " + chunks.Count);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SmallChunk", "MergeForwardReducesCount", "Merge forward absorbs small chunks into neighbors",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> keep = ChunkList(items,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64, MinChunkTokens = 3, SmallChunkMode = SmallChunkModeEnum.Keep });
                            IReadOnlyList<Chunk> merged = ChunkList(items,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64, MinChunkTokens = 3, SmallChunkMode = SmallChunkModeEnum.MergeForward });
                            TestSupport.Assert(merged.Count < keep.Count, "merge forward should reduce chunk count");
                            TestSupport.Assert(merged.Count >= 1, "merge forward should keep at least one chunk");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SmallChunk", "BackwardMergeOfSmallFinal", "A small final chunk merges backward into its predecessor",
                        executeAsync: ct =>
                        {
                            string[] trailing = new[] { "this is a long leading item with several words present", "z" };
                            IReadOnlyList<Chunk> chunks = ChunkList(trailing,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64, MinChunkTokens = 3, SmallChunkMode = SmallChunkModeEnum.MergeForward });
                            TestSupport.Assert(chunks.Count == 1, "small final chunk should merge backward, got " + chunks.Count);
                            TestSupport.Assert(chunks[0].Text.Contains("z"), "merged chunk should retain the trailing content");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
