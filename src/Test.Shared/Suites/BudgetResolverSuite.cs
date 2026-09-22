namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies tokenizer and budget resolution precedence: provider defaults, model heuristics, override, and
    /// calibration.
    /// </summary>
    public static class BudgetResolverSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "BudgetResolver",
                displayName: "Tokenization Profile Resolution",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("BudgetResolver", "OpenAiDefault", "OpenAI resolves to cl100k at 8192",
                        executeAsync: async ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver();
                            ResolvedTokenizationProfile profile = await resolver.ResolveAsync(
                                TokenizerKindEnum.Auto, ApiFormatEnum.OpenAI, "text-embedding-3-small", null, false, ct);
                            TestSupport.Assert(profile.TokenizerKind == TokenizerKindEnum.Cl100kBase, "expected cl100k");
                            TestSupport.Assert(profile.MaxInputTokens == 8192, "expected 8192, got " + profile.MaxInputTokens);
                        }),

                    new TestCaseDescriptor("BudgetResolver", "GeminiDefault", "Gemini resolves to cl100k at 2048",
                        executeAsync: async ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver();
                            ResolvedTokenizationProfile profile = await resolver.ResolveAsync(
                                TokenizerKindEnum.Auto, ApiFormatEnum.Gemini, "text-embedding-004", null, false, ct);
                            TestSupport.Assert(profile.MaxInputTokens == 2048, "expected 2048, got " + profile.MaxInputTokens);
                        }),

                    new TestCaseDescriptor("BudgetResolver", "BertModelHeuristic", "A minilm model resolves to WordPiece at 512",
                        executeAsync: async ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver();
                            ResolvedTokenizationProfile profile = await resolver.ResolveAsync(
                                TokenizerKindEnum.Auto, ApiFormatEnum.Ollama, "all-minilm", null, false, ct);
                            TestSupport.Assert(profile.TokenizerKind == TokenizerKindEnum.BertWordPiece, "expected WordPiece");
                            TestSupport.Assert(profile.MaxInputTokens == 512, "expected 512, got " + profile.MaxInputTokens);
                        }),

                    new TestCaseDescriptor("BudgetResolver", "O200kModel", "A GPT-4o model resolves to the o200k tokenizer",
                        executeAsync: async ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver();
                            ResolvedTokenizationProfile profile = await resolver.ResolveAsync(
                                TokenizerKindEnum.Auto, ApiFormatEnum.OpenAI, "gpt-4o", null, false, ct);
                            TestSupport.Assert(profile.TokenizerKind == TokenizerKindEnum.O200kBase, "expected o200k for gpt-4o");
                        }),

                    new TestCaseDescriptor("BudgetResolver", "BudgetOverride", "An explicit budget override wins",
                        executeAsync: async ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver();
                            ResolvedTokenizationProfile profile = await resolver.ResolveAsync(
                                TokenizerKindEnum.Cl100kBase, ApiFormatEnum.OpenAI, "text-embedding-3-small", 1000, false, ct);
                            TestSupport.Assert(profile.EffectiveInputBudget == 1000, "expected 1000, got " + profile.EffectiveInputBudget);
                            TestSupport.Assert(profile.ProfileSource == TokenizationProfileSourceEnum.Override, "expected override source");
                        }),

                    new TestCaseDescriptor("BudgetResolver", "Calibration", "A calibration probe adjusts the budget",
                        executeAsync: async ct =>
                        {
                            TokenizationProfileResolver resolver = new TokenizationProfileResolver(new FakeCalibrationProbe(333));
                            ResolvedTokenizationProfile profile = await resolver.ResolveAsync(
                                TokenizerKindEnum.Auto, ApiFormatEnum.OpenAI, "text-embedding-3-small", null, true, ct);
                            TestSupport.Assert(profile.EffectiveInputBudget == 333, "expected 333, got " + profile.EffectiveInputBudget);
                            TestSupport.Assert(profile.ProfileSource == TokenizationProfileSourceEnum.Calibration, "expected calibration source");
                        })
                });
        }
    }
}
