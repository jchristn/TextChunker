namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies that text, stream, and file entry points produce identical output for the same content.
    /// </summary>
    public static class InputModeSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "InputMode",
                displayName: "Input Modes",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("InputMode", "TextStreamFileParity", "Text, stream, and file inputs produce identical chunks",
                        executeAsync: async ct =>
                        {
                            string content = TestSupport.MultiParagraph;
                            ChunkingOptions options = new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 40 };
                            Chunker chunker = new Chunker();

                            IReadOnlyList<Chunk> fromText = TestSupport.Collect(chunker.ChunkText(content, options, ct));

                            IReadOnlyList<Chunk> fromStream;
                            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                                fromStream = TestSupport.Collect(chunker.ChunkStream(stream, options, Encoding.UTF8, ct));

                            string path = Path.Combine(Path.GetTempPath(), "textchunker_test_" + Guid.NewGuid().ToString("N") + ".txt");
                            IReadOnlyList<Chunk> fromFile;
                            try
                            {
                                File.WriteAllText(path, content, Encoding.UTF8);
                                fromFile = TestSupport.Collect(chunker.ChunkFile(path, options, null, ct));
                            }
                            finally
                            {
                                if (File.Exists(path)) File.Delete(path);
                            }

                            TestSupport.Assert(fromText.Count == fromStream.Count, "text and stream chunk counts differ");
                            TestSupport.Assert(fromText.Count == fromFile.Count, "text and file chunk counts differ");
                            for (int i = 0; i < fromText.Count; i++)
                            {
                                TestSupport.Assert(string.Equals(fromText[i].Text, fromStream[i].Text, StringComparison.Ordinal), "stream chunk " + i + " differs");
                                TestSupport.Assert(string.Equals(fromText[i].Text, fromFile[i].Text, StringComparison.Ordinal), "file chunk " + i + " differs");
                            }
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputMode", "StructuredRequestParity", "A text ChunkRequest matches ChunkText",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            ChunkingOptions options = new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 32 };

                            IReadOnlyList<Chunk> fromText = TestSupport.Collect(chunker.ChunkText(TestSupport.MultiParagraph, options, ct));
                            SemanticCellRequest request = new SemanticCellRequest { Type = AtomTypeEnum.Text, Text = TestSupport.MultiParagraph };
                            IReadOnlyList<Chunk> fromRequest = TestSupport.Collect(chunker.ChunkRequest(request, options, ct));

                            TestSupport.Assert(fromText.Count == fromRequest.Count, "request chunk count differs from text");
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputMode", "ByteOrderMarkStripped", "A UTF-8 BOM is not carried into the first chunk",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            string content = TestSupport.WordCorpus(120);
                            string path = Path.Combine(Path.GetTempPath(), "textchunker_bom_" + Guid.NewGuid().ToString("N") + ".txt");
                            try
                            {
                                File.WriteAllText(path, content, new UTF8Encoding(true));
                                IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkFile(path, new ChunkingOptions { MaxTokens = 32 }, null, ct));
                                TestSupport.Assert(chunks.Count > 0, "expected chunks");
                                TestSupport.Assert(chunks[0].Text.IndexOf('﻿') < 0, "byte order mark should not appear in the chunk");
                            }
                            finally
                            {
                                if (File.Exists(path)) File.Delete(path);
                            }
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputMode", "StreamLeftOpen", "ChunkStream does not dispose the caller's stream",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(TestSupport.WordCorpus(80))))
                            {
                                IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkStream(stream, new ChunkingOptions { MaxTokens = 32 }, Encoding.UTF8, ct));
                                TestSupport.Assert(chunks.Count > 0, "expected chunks");
                                TestSupport.Assert(stream.CanRead, "stream should remain open after chunking");
                            }
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputMode", "CancellationHonored", "A cancelled token stops enumeration",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            using (CancellationTokenSource cts = new CancellationTokenSource())
                            {
                                cts.Cancel();
                                bool threw = false;
                                try
                                {
                                    await foreach (Chunk _ in chunker.ChunkText(TestSupport.WordCorpus(400), new ChunkingOptions { MaxTokens = 16 }, cts.Token))
                                    {
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    threw = true;
                                }
                                TestSupport.Assert(threw, "expected an OperationCanceledException from a cancelled token");
                            }
                        }),

                    new TestCaseDescriptor("InputMode", "Utf16FileRoundTrip", "A UTF-16 encoded file chunks to the same content as text",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            string content = TestSupport.MultiParagraph;
                            ChunkingOptions options = new ChunkingOptions { Strategy = ChunkStrategyEnum.SentenceBased, MaxTokens = 40 };
                            IReadOnlyList<Chunk> fromText = TestSupport.Collect(chunker.ChunkText(content, options, ct));

                            string path = Path.Combine(Path.GetTempPath(), "textchunker_utf16_" + Guid.NewGuid().ToString("N") + ".txt");
                            try
                            {
                                File.WriteAllText(path, content, Encoding.Unicode);
                                IReadOnlyList<Chunk> fromFile = TestSupport.Collect(
                                    chunker.ChunkFile(path, options, new FileChunkingOptions { Encoding = Encoding.Unicode }, ct));
                                TestSupport.Assert(fromText.Count == fromFile.Count, "UTF-16 file chunk count differs from text");
                                for (int i = 0; i < fromText.Count; i++)
                                    TestSupport.Assert(string.Equals(fromText[i].Text, fromFile[i].Text, StringComparison.Ordinal), "UTF-16 chunk " + i + " differs");
                            }
                            finally
                            {
                                if (File.Exists(path)) File.Delete(path);
                            }
                            await Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputMode", "EmptyStream", "An empty stream yields no chunks",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            using (MemoryStream stream = new MemoryStream(Array.Empty<byte>()))
                            {
                                IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkStream(stream, new ChunkingOptions(), Encoding.UTF8, ct));
                                TestSupport.Assert(chunks.Count == 0, "empty stream should yield no chunks");
                            }
                            await Task.CompletedTask;
                        })
                });
        }
    }
}
