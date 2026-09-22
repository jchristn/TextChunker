namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies chunking through a chunker built with an explicit tokenizer and with a calibration probe.
    /// </summary>
    public static class TokenizerIntegrationSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "TokenizerIntegration",
                displayName: "Chunker Tokenizer Integration",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("TokenizerIntegration", "ExplicitTokenizer", "A chunker built with an explicit tokenizer keeps chunks in budget",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter("o200k_base");
                            Chunker chunker = new Chunker(tokenizer);
                            IReadOnlyList<Chunk> chunks = chunker.Chunk(TestSupport.WordCorpus(200), new ChunkingOptions { MaxTokens = 24 });
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(tokenizer.CountTokens(c.Text) <= 24, "chunk over budget under explicit tokenizer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TokenizerIntegration", "CalibrationProbeThroughChunker", "A chunker with a calibration probe honors the discovered budget",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker(null, new FakeCalibrationProbe(64));
                            IReadOnlyList<Chunk> chunks = TestSupport.Collect(
                                chunker.ChunkText(TestSupport.WordCorpus(600), new ChunkingOptions { MaxTokens = 256, ModelId = "text-embedding-3-small", AllowCalibration = true }, ct));
                            TestSupport.Assert(chunks.Count > 1, "expected multiple chunks");
                            foreach (Chunk c in chunks)
                                TestSupport.Assert(c.TokenCount <= 64, "calibrated budget (64) not honored: " + c.TokenCount);
                            await Task.CompletedTask;
                        })
                });
        }
    }
}
