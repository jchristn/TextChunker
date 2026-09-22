namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using TextChunker.Chunkers;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Golden regression gate for the sizing core. The embedded fixture pins TextChunker's own chunk boundaries.
    /// A change that shifts a boundary shows up here as a failure that must be explained and the fixture re approved.
    /// </summary>
    public static class ParitySuite
    {
        private const string _ResourceName = "Test.Shared.Fixtures.chunking-parity.json";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Parity",
                displayName: "Chunk Boundary Parity",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Parity", "BoundariesMatchGolden", "The sizing core reproduces the golden chunk boundaries exactly",
                        executeAsync: ct =>
                        {
                            List<ParityGoldenCase> cases = LoadGolden();
                            TestSupport.Assert(cases.Count > 0, "golden fixture was empty");

                            ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter("cl100k_base");
                            foreach (ParityGoldenCase golden in cases)
                            {
                                List<string> actual = Chunk(golden, tokenizer);
                                TestSupport.Assert(
                                    actual.Count == golden.Chunks.Count,
                                    "parity mismatch [" + golden.CorpusId + "/" + golden.Strategy + "/max=" + golden.MaxTokens + "/ov=" + golden.Overlap + "]: count " + actual.Count + " != golden " + golden.Chunks.Count);
                                for (int i = 0; i < actual.Count; i++)
                                    TestSupport.Assert(
                                        string.Equals(actual[i], golden.Chunks[i], StringComparison.Ordinal),
                                        "parity mismatch [" + golden.CorpusId + "/" + golden.Strategy + "] chunk " + i + " differs from golden");
                            }
                            return Task.CompletedTask;
                        })
                });
        }

        private static List<string> Chunk(ParityGoldenCase golden, ITokenizerAdapter tokenizer)
        {
            ChunkStrategyEnum strategy = MapStrategy(golden.Strategy);
            ChunkingConfiguration config = new ChunkingConfiguration
            {
                Strategy = strategy,
                FixedTokenCount = golden.MaxTokens,
                OverlapCount = golden.Overlap
            };

            if (strategy == ChunkStrategyEnum.SentenceBased)
                return SentenceChunker.Chunk(golden.Text, config, tokenizer, golden.MaxTokens);
            if (strategy == ChunkStrategyEnum.ParagraphBased)
                return ParagraphChunker.Chunk(golden.Text, config, tokenizer, golden.MaxTokens);
            return FixedTokenChunker.Chunk(golden.Text, config, tokenizer, golden.MaxTokens);
        }

        private static ChunkStrategyEnum MapStrategy(string strategy)
        {
            if (string.Equals(strategy, "SentenceBased", StringComparison.OrdinalIgnoreCase)) return ChunkStrategyEnum.SentenceBased;
            if (string.Equals(strategy, "ParagraphBased", StringComparison.OrdinalIgnoreCase)) return ChunkStrategyEnum.ParagraphBased;
            return ChunkStrategyEnum.FixedTokenCount;
        }

        private static List<ParityGoldenCase> LoadGolden()
        {
            Assembly assembly = typeof(ParitySuite).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream(_ResourceName))
            {
                if (stream == null) throw new Exception("embedded golden fixture not found: " + _ResourceName);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    List<ParityGoldenCase>? cases = JsonSerializer.Deserialize<List<ParityGoldenCase>>(json, options);
                    return cases ?? new List<ParityGoldenCase>();
                }
            }
        }
    }
}
