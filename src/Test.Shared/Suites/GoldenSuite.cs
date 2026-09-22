namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Golden regression gate for the recursive, list, and table strategies through the public API. The fixture
    /// pins TextChunker's own output; a change that shifts a boundary shows up here and must be re approved.
    /// </summary>
    public static class GoldenSuite
    {
        private const string _ResourceName = "Test.Shared.Fixtures.structural-golden.json";

        private static readonly string[] _ListItems = new[]
        {
            "Preheat the oven to 220 degrees.",
            "Combine the dry ingredients.",
            "Fold in the wet ingredients until smooth.",
            "Bake for twenty five minutes."
        };

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Golden",
                displayName: "Structural Golden",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Golden", "RecursiveProse", "Recursive prose reproduces the golden boundaries",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> chunks = chunker.Chunk(TestSupport.MultiParagraph, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 24 });
                            AssertMatches("recursive-prose", chunks);
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Golden", "ListEntry", "List entry reproduces the golden boundaries",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkList(_ListItems, false, new ChunkingOptions { Strategy = ChunkStrategyEnum.ListEntry, MaxTokens = 64 }, ct));
                            AssertMatches("list-entry", chunks);
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Golden", "TableRows", "Table rows reproduce the golden boundaries",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Name", "Role" },
                                new List<string> { "Ada", "Engineer" },
                                new List<string> { "Bob", "Designer" }
                            };
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(chunker.ChunkTable(rows, new ChunkingOptions { Strategy = ChunkStrategyEnum.RowWithHeaders, MaxTokens = 128 }, ct));
                            AssertMatches("table-rows", chunks);
                            return System.Threading.Tasks.Task.CompletedTask;
                        })
                });
        }

        private static void AssertMatches(string key, IReadOnlyList<Chunk> chunks)
        {
            Dictionary<string, List<string>> golden = LoadGolden();
            TestSupport.Assert(golden.ContainsKey(key), "golden fixture missing key " + key);
            List<string> expected = golden[key];
            TestSupport.Assert(chunks.Count == expected.Count, "golden mismatch [" + key + "]: count " + chunks.Count + " != " + expected.Count);
            for (int i = 0; i < expected.Count; i++)
                TestSupport.Assert(string.Equals(chunks[i].Text, expected[i], StringComparison.Ordinal), "golden mismatch [" + key + "] chunk " + i);
        }

        private static Dictionary<string, List<string>> LoadGolden()
        {
            Assembly assembly = typeof(GoldenSuite).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream(_ResourceName))
            {
                if (stream == null) throw new Exception("embedded golden fixture not found: " + _ResourceName);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    Dictionary<string, List<string>>? golden = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    return golden ?? new Dictionary<string, List<string>>();
                }
            }
        }
    }
}
