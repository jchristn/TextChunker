namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Exceptions;
    using TextChunker.Models;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the public contract rejects invalid arguments and surfaces the right exception types.
    /// </summary>
    public static class NegativeArgumentSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "NegativeArgument",
                displayName: "Argument Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("NegativeArgument", "NullStream", "ChunkStream rejects a null stream",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            TestSupport.ExpectThrowsOnEnumerate<ArgumentNullException>(chunker.ChunkStream(null!), "expected ArgumentNullException for null stream");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "NullFilename", "ChunkFile rejects a null filename",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            TestSupport.ExpectThrowsOnEnumerate<ArgumentNullException>(chunker.ChunkFile(null!), "expected ArgumentNullException for null filename");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "MissingFile", "ChunkFile rejects a missing file",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            string path = Path.Combine(Path.GetTempPath(), "textchunker_missing_" + Guid.NewGuid().ToString("N") + ".txt");
                            TestSupport.ExpectThrowsOnEnumerate<FileNotFoundException>(chunker.ChunkFile(path), "expected FileNotFoundException for missing file");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "NullList", "ChunkList rejects a null enumerable",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            TestSupport.ExpectThrowsOnEnumerate<ArgumentNullException>(chunker.ChunkList(null!), "expected ArgumentNullException for null list");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "NullTable", "ChunkTable rejects a null table",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            TestSupport.ExpectThrowsOnEnumerate<ArgumentNullException>(chunker.ChunkTable(null!), "expected ArgumentNullException for null table");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "NullRequest", "ChunkRequest rejects a null request",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            TestSupport.ExpectThrowsOnEnumerate<ArgumentNullException>(chunker.ChunkRequest(null!), "expected ArgumentNullException for null request");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "NullMlTokenizer", "MlTokenizerAdapter rejects a null tokenizer",
                        executeAsync: ct =>
                        {
                            TestSupport.ExpectThrows<ArgumentNullException>(() => new MlTokenizerAdapter(null!), "expected ArgumentNullException for null tokenizer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "FactoryRejectsAuto", "The adapter factory rejects an unresolved tokenizer kind",
                        executeAsync: ct =>
                        {
                            ResolvedTokenizationProfile profile = new ResolvedTokenizationProfile { TokenizerKind = TokenizerKindEnum.Auto };
                            TestSupport.ExpectThrows<TokenizerResolutionException>(() => TokenizerAdapterFactory.Create(profile), "expected TokenizerResolutionException for Auto kind");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "ResolverWrapsProbeFailure", "The resolver wraps a throwing calibration probe",
                        executeAsync: ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver(new ThrowingCalibrationProbe());
                            TestSupport.ExpectThrows<TokenizerResolutionException>(
                                () => resolver.ResolveAsync(TokenizerKindEnum.Auto, ApiFormatEnum.OpenAI, "text-embedding-3-small", null, true, ct).GetAwaiter().GetResult(),
                                "expected TokenizerResolutionException from a throwing probe");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NegativeArgument", "MalformedRegexThrows", "A malformed regex pattern surfaces an exception",
                        executeAsync: ct =>
                        {
                            Chunker chunker = new Chunker();
                            TestSupport.ExpectThrowsOnEnumerate<ArgumentException>(
                                chunker.ChunkText("some text here", new ChunkingOptions { Strategy = ChunkStrategyEnum.RegexBased, RegexPattern = "(" }),
                                "expected an exception for a malformed regex pattern");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
