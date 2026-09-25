namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Serialization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the JSON serialization contract round trips and rejects unknown enum values.
    /// </summary>
    public static class SerializationSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Serialization",
                displayName: "JSON Serialization",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Serialization", "ChunkRoundTrip", "A chunk round trips through the shared contract",
                        executeAsync: ct =>
                        {
                            Chunk chunk = new Chunk
                            {
                                Text = "hello",
                                Position = 3,
                                TokenCount = 1,
                                Strategy = ChunkStrategyEnum.SentenceBased
                            };
                            string json = ChunkerJson.Serialize(chunk);
                            Chunk? restored = ChunkerJson.Deserialize<Chunk>(json);
                            TestSupport.Assert(restored != null, "deserialization returned null");
                            TestSupport.Assert(restored!.Position == 3, "position did not round trip");
                            TestSupport.Assert(restored.Strategy == ChunkStrategyEnum.SentenceBased, "strategy did not round trip");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Serialization", "EnumSerializesAsString", "Enums serialize as their string names",
                        executeAsync: ct =>
                        {
                            string json = ChunkerJson.Serialize(new Chunk { Strategy = ChunkStrategyEnum.ParagraphBased });
                            TestSupport.Assert(json.Contains("ParagraphBased"), "enum should serialize as a string name");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Serialization", "UnknownEnumRejected", "An unknown enum string is rejected on read",
                        executeAsync: ct =>
                        {
                            string json = "{\"strategy\":\"NotARealStrategy\"}";
                            bool threw = false;
                            try
                            {
                                ChunkerJson.Deserialize<Chunk>(json);
                            }
                            catch (System.Text.Json.JsonException)
                            {
                                threw = true;
                            }
                            TestSupport.Assert(threw, "expected a JsonException for an unknown enum value");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Serialization", "OptionsRoundTrip", "Chunking options round trip through the shared contract",
                        executeAsync: ct =>
                        {
                            ChunkingOptions options = new ChunkingOptions
                            {
                                Strategy = ChunkStrategyEnum.Recursive,
                                MaxTokens = 384,
                                OverlapCount = 32,
                                Format = ContentFormatEnum.Markdown,
                                HierarchyAware = true,
                                ModelId = "all-minilm",
                                SafetyMarginTokens = 3,
                                SafetyMarginPercentage = 0.04
                            };
                            string json = ChunkerJson.Serialize(options);
                            ChunkingOptions? restored = ChunkerJson.Deserialize<ChunkingOptions>(json);
                            TestSupport.Assert(restored != null, "deserialization returned null");
                            TestSupport.Assert(restored!.Strategy == ChunkStrategyEnum.Recursive, "strategy did not round trip");
                            TestSupport.Assert(restored.MaxTokens == 384, "max tokens did not round trip");
                            TestSupport.Assert(restored.Format == ContentFormatEnum.Markdown, "format did not round trip");
                            TestSupport.Assert(restored.ModelId == "all-minilm", "model id did not round trip");
                            TestSupport.Assert(restored.SafetyMarginTokens == 3, "safety margin tokens did not round trip");
                            TestSupport.Assert(restored.SafetyMarginPercentage == 0.04, "safety margin percentage did not round trip");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Serialization", "ResultRoundTrip", "A chunking result round trips through the shared contract",
                        executeAsync: ct =>
                        {
                            ChunkingResult result = new ChunkingResult();
                            result.Chunks.Add(new Chunk { Text = "hello", Position = 0, TokenCount = 1 });
                            result.Chunks.Add(new Chunk { Text = "world", Position = 1, TokenCount = 1 });
                            result.Diagnostic.ChunkCount = 2;
                            result.Diagnostic.TotalTokens = 2;

                            string json = ChunkerJson.Serialize(result);
                            ChunkingResult? restored = ChunkerJson.Deserialize<ChunkingResult>(json);
                            TestSupport.Assert(restored != null, "deserialization returned null");
                            TestSupport.Assert(restored!.Chunks.Count == 2, "chunk count did not round trip");
                            TestSupport.Assert(restored.Diagnostic.ChunkCount == 2, "diagnostic did not round trip");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
