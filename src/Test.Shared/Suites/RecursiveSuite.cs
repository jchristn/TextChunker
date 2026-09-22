namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the recursive separator splitter across default, markdown, and custom separator ladders.
    /// </summary>
    public static class RecursiveSuite
    {
        private const string _Markdown =
            "# Title\nIntro paragraph with several words to fill space and more and more.\n\n"
            + "## Section A\nContent for section A that runs on for a while with additional filler text here.\n\n"
            + "## Section B\nContent for section B that also runs on for a while with additional filler text here.";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Recursive",
                displayName: "Recursive Splitter",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Recursive", "ProseInBudget", "Recursive prose chunking stays within budget and produces multiple chunks",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                TestSupport.MultiParagraph,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 32 });
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 32, "recursive chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Recursive", "MarkdownFormat", "Recursive markdown format keeps sections within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                _Markdown,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, Format = ContentFormatEnum.Markdown, MaxTokens = 40 });
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks for markdown");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 40, "markdown chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Recursive", "CSharpFormat", "Recursive C# format keeps declarations within budget",
                        executeAsync: ct =>
                        {
                            string code =
                                "namespace Demo\n{\n    public class Foo\n    {\n        public int Add(int a, int b) { return a + b; }\n"
                                + "        public int Sub(int a, int b) { return a - b; }\n    }\n\n    public class Bar\n    {\n"
                                + "        public string Greet(string name) { return \"hello \" + name; }\n    }\n}";
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                code,
                                new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, Format = ContentFormatEnum.CSharp, MaxTokens = 24 });
                            TestSupport.Assert(chunks.Count > 1, "expected multiple code chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 24, "code chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Recursive", "LanguageFormats", "Recursive Python, JavaScript, and Java ladders keep code within budget",
                        executeAsync: ct =>
                        {
                            string python = "class A:\n    def one(self):\n        return 1\n\n    def two(self):\n        return 2\n\nclass B:\n    def three(self):\n        return 3";
                            string javascript = "function one() { return 1; }\n\nfunction two() { return 2; }\n\nclass Widget {\n  render() { return 'x'; }\n}";
                            string java = "public class A {\n    public int one() { return 1; }\n    public int two() { return 2; }\n}\n\nclass B {\n    int three() { return 3; }\n}";

                            foreach (ContentFormatEnum format in new[] { ContentFormatEnum.Python, ContentFormatEnum.JavaScript, ContentFormatEnum.Java })
                            {
                                string code = format == ContentFormatEnum.Python ? python : format == ContentFormatEnum.JavaScript ? javascript : java;
                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(code, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, Format = format, MaxTokens = 16 });
                                TestSupport.Assert(chunks.Count > 1, "expected multiple chunks for " + format);
                                foreach (Chunk c in chunks)
                                    TestSupport.Assert(c.TokenCount <= 16, format + " chunk over budget: " + c.TokenCount);
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Recursive", "CustomSeparators", "Recursive respects a custom separator ladder",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                "alpha beta gamma||delta epsilon zeta||eta theta iota",
                                new ChunkingOptions
                                {
                                    Strategy = ChunkStrategyEnum.Recursive,
                                    MaxTokens = 6,
                                    Separators = new List<string> { "||", " ", "" }
                                });
                            TestSupport.Assert(chunks.Count >= 3, "expected at least three chunks, got " + chunks.Count);
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 6, "custom separator chunk over budget: " + c.TokenCount);
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
