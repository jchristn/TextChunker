namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Tokenization;

    /// <summary>
    /// A calibration probe that returns a fixed budget without any network access, for tests.
    /// </summary>
    public sealed class FakeCalibrationProbe : ITokenizerCalibrationProbe
    {
        private readonly int _Budget;

        /// <summary>
        /// Initialize a probe that reports the supplied budget.
        /// </summary>
        /// <param name="budget">Effective input budget to report.</param>
        public FakeCalibrationProbe(int budget)
        {
            _Budget = budget;
        }

        /// <inheritdoc />
        public Task<TokenizationCalibrationResult?> CalibrateAsync(
            ResolvedTokenizationProfile provisionalProfile,
            ITokenizerAdapter tokenizer,
            CancellationToken token = default)
        {
            TokenizationCalibrationResult result = new TokenizationCalibrationResult
            {
                EffectiveInputBudget = _Budget,
                BatchLimitMode = BatchLimitModeEnum.PerInput
            };
            return Task.FromResult<TokenizationCalibrationResult?>(result);
        }
    }
}
