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
    /// Verifies chunk metadata: parent identity, ordinal continuity, token counts, offsets, and hashes.
    /// </summary>
    public static class MetadataSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Metadata",
                displayName: "Chunk Metadata",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Metadata", "OrdinalsContiguous", "Ordinals are contiguous from zero",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(200), new ChunkingOptions { MaxTokens = 32 });
                            for (int i = 0; i < chunks.Count; i++)
                                TestSupport.Assert(chunks[i].Position == i, "position mismatch at " + i);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "ParentGuidStamped", "A supplied ParentGUID is stamped on every chunk",
                        executeAsync: ct =>
                        {
                            Guid parent = Guid.NewGuid();
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(100), new ChunkingOptions { MaxTokens = 32, ParentGUID = parent });
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.ParentGUID == parent, "parent guid not stamped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "OffsetsIndexBackToSource", "Text strategy offsets index back to the exact substring",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.WordCorpus(120);
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { MaxTokens = 24, ComputeOffsets = true });
                            foreach (Chunk c in chunks)
                            {
                                if (c.StartOffset < 0) continue;
                                string sub = source.Substring(c.StartOffset, c.EndOffset - c.StartOffset);
                                TestSupport.Assert(string.Equals(sub, c.Text, StringComparison.Ordinal), "offset substring did not match chunk text");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "HashesComputedWhenRequested", "Hashes are computed only when requested",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> withHashes = TestSupport.Chunk(TestSupport.WordCorpus(40), new ChunkingOptions { MaxTokens = 32, ComputeHashes = true });
                            foreach (Chunk c in withHashes)
                            {
                                TestSupport.Assert(c.MD5Hash != null && c.MD5Hash.Length == 16, "expected MD5");
                                TestSupport.Assert(c.SHA256Hash != null && c.SHA256Hash.Length == 32, "expected SHA256");
                            }

                            IReadOnlyList<Chunk> without = TestSupport.Chunk(TestSupport.WordCorpus(40), new ChunkingOptions { MaxTokens = 32, ComputeHashes = false });
                            foreach (Chunk c in without)
                                TestSupport.Assert(c.MD5Hash == null, "did not expect hashes");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "ContextPrefixApplied", "Context prefix is applied and kept within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.WordCorpus(120),
                                new ChunkingOptions { MaxTokens = 32, ContextPrefix = "PREFIX: " });
                            foreach (Chunk c in chunks)
                            {
                                TestSupport.Assert(c.Text.StartsWith("PREFIX: ", StringComparison.Ordinal), "prefix not applied");
                                TestSupport.Assert(c.TokenCount <= 32, "prefixed chunk exceeded budget: " + c.TokenCount);
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "LabelsAndTagsEchoed", "Request labels and tags are echoed onto every chunk",
                        executeAsync: ct =>
                        {
                            ContentRequest request = new ContentRequest
                            {
                                Type = ContentTypeEnum.Text,
                                Text = TestSupport.WordCorpus(120),
                                Labels = new List<string> { "alpha", "beta" },
                                Tags = new Dictionary<string, string> { { "source", "unit-test" }, { "lang", "en" } }
                            };
                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkRequest(request, new ChunkingOptions { MaxTokens = 24 }, ct));
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks");
                            foreach (Chunk c in chunks)
                            {
                                TestSupport.Assert(c.Labels.Count == 2 && c.Labels.Contains("alpha") && c.Labels.Contains("beta"), "labels not echoed");
                                TestSupport.Assert(c.Tags.Count == 2 && c.Tags["source"] == "unit-test" && c.Tags["lang"] == "en", "tags not echoed");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "LabelsTagsEmptyWithoutRequest", "Chunks carry empty labels and tags when none are supplied",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(40), new ChunkingOptions { MaxTokens = 32 });
                            foreach (Chunk c in chunks)
                            {
                                TestSupport.Assert(c.Labels != null && c.Labels.Count == 0, "expected empty labels");
                                TestSupport.Assert(c.Tags != null && c.Tags.Count == 0, "expected empty tags");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "TokenCountsDisabled", "Token counts are zero when computation is disabled",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(120), new ChunkingOptions { MaxTokens = 24, ComputeTokenCounts = false });
                            TestSupport.Assert(chunks.Count > 0, "expected chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount == 0, "token count should be 0 when disabled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "OffsetsDisabled", "Offsets are -1 when computation is disabled",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(TestSupport.WordCorpus(120), new ChunkingOptions { MaxTokens = 24, ComputeOffsets = false });
                            foreach (Chunk c in chunks)
                            {
                                TestSupport.Assert(c.StartOffset == -1, "start offset should be -1 when disabled");
                                TestSupport.Assert(c.EndOffset == -1, "end offset should be -1 when disabled");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "TrimWhitespaceDisabled", "Whitespace is preserved when trimming is disabled",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> trimmed = TestSupport.Chunk(TestSupport.WordCorpus(120), new ChunkingOptions { MaxTokens = 24, TrimWhitespace = true });
                            IReadOnlyList<Chunk> raw = TestSupport.Chunk(TestSupport.WordCorpus(120), new ChunkingOptions { MaxTokens = 24, TrimWhitespace = false });
                            TestSupport.Assert(trimmed.Count == raw.Count, "chunk count should not depend on trimming");
                            bool anyLeadingSpace = false;
                            foreach (Chunk c in raw)
                                if (c.Text.Length > 0 && char.IsWhiteSpace(c.Text[0])) anyLeadingSpace = true;
                            TestSupport.Assert(anyLeadingSpace, "expected at least one untrimmed chunk to retain leading whitespace");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Metadata", "SerializedOffsetsAreNegative", "List and table serialized chunks report -1 offsets",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> listChunks = TestSupport.Collect(
                                chunker.ChunkList(new[] { "first item", "second item", "third item" }, false,
                                    new ChunkingOptions { Strategy = ChunkStrategyEnum.WholeList, MaxTokens = 128 }, ct));
                            foreach (Chunk c in listChunks)
                                TestSupport.Assert(c.StartOffset == -1 && c.EndOffset == -1, "serialized list chunk should have -1 offsets");

                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Name", "Role" },
                                new List<string> { "Ada", "Engineer" }
                            };
                            IReadOnlyList<Chunk> tableChunks = TestSupport.Collect(
                                chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.RowWithHeaders, MaxTokens = 128 }, ct));
                            foreach (Chunk c in tableChunks)
                                TestSupport.Assert(c.StartOffset == -1 && c.EndOffset == -1, "serialized table chunk should have -1 offsets");
                            await Task.CompletedTask;
                        })
                });
        }
    }
}
