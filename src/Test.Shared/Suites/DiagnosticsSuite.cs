namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the materialized result and its run level diagnostics.
    /// </summary>
    public static class DiagnosticsSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Diagnostics",
                displayName: "Result Diagnostics",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Diagnostics", "ChunkToResultDiagnostics", "ChunkToResultAsync reports accurate diagnostics",
                        executeAsync: async ct =>
                        {
                            string source = TestSupport.WordCorpus(300);
                            Chunker chunker = new Chunker();
                            ChunkingResult result = await chunker.ChunkToResultAsync(source, new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 32 }, ct);

                            TestSupport.Assert(result.Chunks.Count > 1, "expected multiple chunks");
                            TestSupport.Assert(result.Diagnostic.ChunkCount == result.Chunks.Count, "diagnostic chunk count mismatch");
                            TestSupport.Assert(result.Diagnostic.Strategy == ChunkStrategyEnum.FixedTokenCount, "diagnostic strategy mismatch");
                            TestSupport.Assert(result.Diagnostic.EffectiveTokenBudget > 0, "expected a positive budget");
                            TestSupport.Assert(result.Diagnostic.InputCharacterCount == source.Length, "input character count mismatch");
                            TestSupport.Assert(!string.IsNullOrEmpty(result.Diagnostic.TokenizerModel), "expected a tokenizer model");

                            int total = 0;
                            int max = 0;
                            foreach (Chunk c in result.Chunks)
                            {
                                total += c.TokenCount;
                                if (c.TokenCount > max) max = c.TokenCount;
                            }
                            TestSupport.Assert(result.Diagnostic.TotalTokens == total, "total token mismatch");
                            TestSupport.Assert(result.Diagnostic.MaxChunkTokens == max, "max token mismatch");
                        }),

                    new TestCaseDescriptor("Diagnostics", "ProfileSourceForModel", "The diagnostic reports the resolved profile source for a model",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            ChunkingResult result = await chunker.ChunkToResultAsync(TestSupport.WordCorpus(80), new ChunkingOptions { MaxTokens = 32, ModelId = "all-minilm" }, ct);
                            TestSupport.Assert(result.Diagnostic.TokenizerKind == TokenizerKindEnum.BertWordPiece, "expected WordPiece for minilm");
                            return;
                        })
                });
        }
    }
}
