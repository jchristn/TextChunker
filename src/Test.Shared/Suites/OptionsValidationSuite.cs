namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Exceptions;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Verifies option guard clauses reject invalid values with the right exception types.
    /// </summary>
    public static class OptionsValidationSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "OptionsValidation",
                displayName: "Options Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("OptionsValidation", "MaxTokensBelowOne", "MaxTokens below 1 is rejected",
                        executeAsync: ct =>
                        {
                            ExpectInvalid(() => new ChunkingOptions { MaxTokens = 0 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("OptionsValidation", "OverlapPercentageOutOfRange", "OverlapPercentage outside 0 to 1 is rejected",
                        executeAsync: ct =>
                        {
                            ExpectInvalid(() => new ChunkingOptions { OverlapPercentage = 1.5 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("OptionsValidation", "NegativeMaxInputCharacters", "Negative MaxInputCharacters is rejected",
                        executeAsync: ct =>
                        {
                            ExpectInvalid(() => new ChunkingOptions { MaxInputCharacters = -1 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("OptionsValidation", "RegexTimeoutBelowOne", "RegexTimeoutMilliseconds below 1 is rejected",
                        executeAsync: ct =>
                        {
                            ExpectInvalid(() => new ChunkingOptions { RegexTimeoutMilliseconds = 0 });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("OptionsValidation", "OversizeInputThrows", "Input over MaxInputCharacters throws when chunked",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            bool threw = false;
                            try
                            {
                                await foreach (Chunk _ in chunker.ChunkText(new string('a', 100), new ChunkingOptions { MaxInputCharacters = 10 }, ct))
                                {
                                }
                            }
                            catch (InvalidChunkingOptionsException)
                            {
                                threw = true;
                            }
                            TestSupport.Assert(threw, "expected an InvalidChunkingOptionsException for oversize input");
                        }),

                    new TestCaseDescriptor("OptionsValidation", "RegexRequiresPattern", "RegexBased without a pattern is rejected",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            bool threw = false;
                            try
                            {
                                await foreach (Chunk _ in chunker.ChunkText("some text", new ChunkingOptions { Strategy = ChunkStrategyEnum.RegexBased }, ct))
                                {
                                }
                            }
                            catch (InvalidChunkingOptionsException)
                            {
                                threw = true;
                            }
                            TestSupport.Assert(threw, "expected an InvalidChunkingOptionsException when RegexPattern is missing");
                        })
                });
        }

        private static void ExpectInvalid(Func<ChunkingOptions> build)
        {
            bool threw = false;
            try
            {
                build();
            }
            catch (InvalidChunkingOptionsException)
            {
                threw = true;
            }
            TestSupport.Assert(threw, "expected an InvalidChunkingOptionsException");
        }
    }
}
