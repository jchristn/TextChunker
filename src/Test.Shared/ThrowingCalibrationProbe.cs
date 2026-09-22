namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Models;
    using TextChunker.Tokenization;

    /// <summary>
    /// A calibration probe that always throws, to exercise the resolver's error wrapping.
    /// </summary>
    public sealed class ThrowingCalibrationProbe : ITokenizerCalibrationProbe
    {
        /// <inheritdoc />
        public Task<TokenizationCalibrationResult?> CalibrateAsync(
            ResolvedTokenizationProfile provisionalProfile,
            ITokenizerAdapter tokenizer,
            CancellationToken token = default)
        {
            throw new InvalidOperationException("probe failure");
        }
    }
}
