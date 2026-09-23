namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Exercises large inputs across the stream, file, list, table, and structured request entry points, so the
    /// budget guarantee and ordinal continuity hold at scale, not only for medium sized text.
    /// </summary>
    public static class LargeInputSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "LargeInput",
                displayName: "Large Inputs",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("LargeInput", "LargeStream", "A large stream chunks within budget with contiguous ordinals",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            string content = TestSupport.WordCorpus(30000);
                            IReadOnlyList<Chunk> chunks;
                            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                                chunks = TestSupport.Collect(chunker.ChunkStream(stream, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 256 }, Encoding.UTF8, ct));

                            TestSupport.Assert(chunks.Count > 50, "expected many chunks from a large stream, got " + chunks.Count);
                            for (int i = 0; i < chunks.Count; i++)
                            {
                                TestSupport.Assert(chunks[i].Position == i, "stream ordinal mismatch at " + i);
                                TestSupport.Assert(chunks[i].TokenCount <= 256, "stream chunk over budget: " + chunks[i].TokenCount);
                            }
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LargeInput", "LargeFile", "A large file chunks within budget",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            string content = TestSupport.WordCorpus(30000);
                            string path = Path.Combine(Path.GetTempPath(), "textchunker_large_" + Guid.NewGuid().ToString("N") + ".txt");
                            try
                            {
                                File.WriteAllText(path, content, Encoding.UTF8);
                                IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkFile(path, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 256 }, null, ct));
                                TestSupport.Assert(chunks.Count > 50, "expected many chunks from a large file, got " + chunks.Count);
                                foreach (Chunk c in chunks)
                                    TestSupport.Assert(c.TokenCount <= 256, "file chunk over budget: " + c.TokenCount);
                            }
                            finally
                            {
                                if (File.Exists(path)) File.Delete(path);
                            }
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LargeInput", "LargeList", "A list of a thousand items chunks per entry within budget",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<string> items = Enumerable.Range(1, 1000).Select(i => "item" + i).ToList();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkList(items, false, new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64 }, ct));
                            TestSupport.Assert(chunks.Count == 1000, "expected one chunk per item, got " + chunks.Count);
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 64, "list chunk over budget: " + c.TokenCount);
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LargeInput", "LargeTable", "A table of five hundred rows chunks per row within budget",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>> { new List<string> { "Id", "Name" } };
                            for (int i = 1; i <= 500; i++)
                                rows.Add(new List<string> { i.ToString(), "person" + i });

                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.RowWithHeaders, MaxTokens = 128 }, ct));
                            TestSupport.Assert(chunks.Count == 500, "expected one chunk per data row, got " + chunks.Count);
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 128, "table chunk over budget: " + c.TokenCount);
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LargeInput", "LargeRequestText", "A large text request chunks within budget",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            ContentRequest request = new ContentRequest { Type = ContentTypeEnum.Text, Text = TestSupport.WordCorpus(30000) };
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkRequest(request, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 256 }, ct));
                            TestSupport.Assert(chunks.Count > 50, "expected many chunks from a large request, got " + chunks.Count);
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 256, "request chunk over budget: " + c.TokenCount);
                            await System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LargeInput", "RequestWithManyChildren", "A request with a hundred child content items chunks all of them within budget",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            ContentRequest root = new ContentRequest { Type = ContentTypeEnum.Text, Text = "Root introduction with several words to fill a chunk." };
                            List<ContentRequest> children = new List<ContentRequest>();
                            for (int i = 0; i < 100; i++)
                                children.Add(new ContentRequest { Type = ContentTypeEnum.Text, Text = "Child section " + i + " has enough words present here to produce at least one chunk of content." });
                            root.Children = children;

                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkRequest(root, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 64 }, ct));
                            TestSupport.Assert(chunks.Count >= 100, "expected at least a chunk per child, got " + chunks.Count);
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 64, "child chunk over budget: " + c.TokenCount);
                            await System.Threading.Tasks.Task.CompletedTask;
                        })
                });
        }
    }
}
