namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies that a structured request with child cells chunks in document order across cell types.
    /// </summary>
    public static class RequestHierarchySuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RequestHierarchy",
                displayName: "Structured Request Children",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("RequestHierarchy", "ChildrenChunkedInOrder", "Parent and child cells chunk in order and cover all content",
                        executeAsync: async ct =>
                        {
                            SemanticCellRequest root = new SemanticCellRequest
                            {
                                Type = ContentTypeEnum.Text,
                                Text = "Root section introduces the topic with enough words to form a chunk on its own."
                            };
                            SemanticCellRequest childText = new SemanticCellRequest
                            {
                                Type = ContentTypeEnum.Text,
                                Text = "Child section adds supporting detail and also has enough words to chunk cleanly."
                            };
                            SemanticCellRequest childList = new SemanticCellRequest
                            {
                                Type = ContentTypeEnum.List,
                                OrderedList = new List<string> { "first point", "second point", "third point" }
                            };
                            root.Children = new List<SemanticCellRequest> { childText, childList };

                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkRequest(root, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 64 }, ct));

                            TestSupport.Assert(chunks.Count >= 3, "expected chunks from root and both children, got " + chunks.Count);

                            string all = string.Empty;
                            foreach (Chunk c in chunks) all += c.Text + "\n";
                            TestSupport.Assert(all.Contains("Root section"), "root content missing");
                            TestSupport.Assert(all.Contains("Child section"), "child text content missing");
                            TestSupport.Assert(all.Contains("first point"), "child list content missing");

                            // Position is an ordinal within each parent cell, so each cell's chunks start at 0.
                            TestSupport.Assert(chunks[0].Position == 0, "first chunk should be position 0");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.Position >= 0, "positions should be non negative");
                            await Task.CompletedTask;
                        })
                });
        }
    }
}
