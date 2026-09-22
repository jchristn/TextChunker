namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies hierarchy aware chunking builds breadcrumb header context.
    /// </summary>
    public static class HierarchySuite
    {
        private const string _Markdown =
            "# Guide\nWelcome to the guide.\n## Setup\nInstall the package.\n### Windows\nRun the installer.\n## Usage\nCall the API.";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Hierarchy",
                displayName: "Hierarchy Aware Chunking",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Hierarchy", "BreadcrumbContext", "Hierarchy aware chunking stamps a breadcrumb header context",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                _Markdown,
                                new ChunkingOptions { HierarchyAware = true, MaxTokens = 64 });

                            TestSupport.Assert(chunks.Count >= 4, "expected a chunk per section, got " + chunks.Count);

                            bool foundNested = false;
                            foreach (Chunk c in chunks)
                            {
                                if (c.HeaderContext != null && c.HeaderContext.Contains("Guide > Setup > Windows"))
                                    foundNested = true;
                            }
                            TestSupport.Assert(foundNested, "expected a nested breadcrumb Guide > Setup > Windows");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hierarchy", "ContextualizeHeaders", "Contextualization prepends the breadcrumb into the chunk text within budget",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                _Markdown,
                                new ChunkingOptions { HierarchyAware = true, ContextualizeHeaders = true, MaxTokens = 64 });

                            bool foundPrepended = false;
                            foreach (Chunk c in chunks)
                            {
                                if (c.HeaderContext != null && c.Text.StartsWith(c.HeaderContext, System.StringComparison.Ordinal))
                                    foundPrepended = true;
                                TestSupport.Assert(c.TokenCount <= 64, "contextualized chunk over budget: " + c.TokenCount);
                            }
                            TestSupport.Assert(foundPrepended, "expected at least one chunk with its breadcrumb prepended");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hierarchy", "ContentBeforeFirstHeader", "Content before the first header is retained under the root",
                        executeAsync: ct =>
                        {
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(
                                "Intro paragraph before any header.\n# Section\nBody.",
                                new ChunkingOptions { HierarchyAware = true, MaxTokens = 64 });
                            bool foundIntro = false;
                            foreach (Chunk c in chunks)
                                if (c.Text.Contains("Intro paragraph")) foundIntro = true;
                            TestSupport.Assert(foundIntro, "intro content before the first header should be retained");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
