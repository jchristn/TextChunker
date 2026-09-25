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

                    new TestCaseDescriptor("OptionsValidation", "SafetyMarginOutOfRange", "A negative safety margin or a percentage outside 0 to 0.5 is rejected",
                        executeAsync: ct =>
                        {
                            ExpectInvalid(() => new ChunkingOptions { SafetyMarginTokens = -1 });
                            ExpectInvalid(() => new ChunkingOptions { SafetyMarginPercentage = -0.01 });
                            ExpectInvalid(() => new ChunkingOptions { SafetyMarginPercentage = 0.51 });
                            ChunkingOptions valid = new ChunkingOptions { SafetyMarginTokens = 0, SafetyMarginPercentage = 0.5 };
                            TestSupport.Assert(valid.SafetyMarginPercentage == 0.5 && valid.SafetyMarginTokens == 0, "boundary values should be accepted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("OptionsValidation", "SafetyMarginShrinksModelBudget", "The safety margin comes off the model budget, not off a smaller MaxTokens",
                        executeAsync: async ct =>
                        {
                            Chunker chunker = new Chunker();
                            string source = TestSupport.WordCorpus(800);

                            ChunkingResult plain = await chunker.ChunkToResultAsync(source, new ChunkingOptions { ModelId = "all-minilm", MaxTokens = 512 }, ct);
                            TestSupport.Assert(plain.Diagnostic.EffectiveTokenBudget == 254, "all-minilm should resolve to 254, got " + plain.Diagnostic.EffectiveTokenBudget);

                            ChunkingResult percent = await chunker.ChunkToResultAsync(source, new ChunkingOptions { ModelId = "all-minilm", MaxTokens = 512, SafetyMarginPercentage = 0.04 }, ct);
                            TestSupport.Assert(percent.Diagnostic.EffectiveTokenBudget == 243, "4 percent of 254 rounds up to 11, leaving 243, got " + percent.Diagnostic.EffectiveTokenBudget);
                            foreach (Chunk c in percent.Chunks)
                                TestSupport.Assert(c.TokenCount <= 243, "chunk over the margin adjusted budget: " + c.TokenCount);

                            ChunkingResult both = await chunker.ChunkToResultAsync(source, new ChunkingOptions { ModelId = "all-minilm", MaxTokens = 512, SafetyMarginTokens = 4, SafetyMarginPercentage = 0.04 }, ct);
                            TestSupport.Assert(both.Diagnostic.EffectiveTokenBudget == 239, "token and percentage margins should add, got " + both.Diagnostic.EffectiveTokenBudget);

                            ChunkingResult smallMax = await chunker.ChunkToResultAsync(source, new ChunkingOptions { ModelId = "all-minilm", MaxTokens = 100, SafetyMarginTokens = 20 }, ct);
                            TestSupport.Assert(smallMax.Diagnostic.EffectiveTokenBudget == 100, "a MaxTokens below the adjusted model budget should be unaffected");

                            ChunkingResult huge = await chunker.ChunkToResultAsync("short text here", new ChunkingOptions { ModelId = "all-minilm", SafetyMarginTokens = 100000 }, ct);
                            TestSupport.Assert(huge.Diagnostic.EffectiveTokenBudget == 1, "a margin larger than the budget should clamp to 1");
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
