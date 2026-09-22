namespace TextChunker.Tokenization
{
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Models;

    /// <summary>
    /// Optional hook that measures a live embedding endpoint to discover its true accepted token budget.
    /// The core library never opens network connections. A caller that wants live calibration supplies an
    /// implementation that owns the transport, keeping the core package free of an HTTP dependency.
    /// </summary>
    public interface ITokenizerCalibrationProbe
    {
        /// <summary>
        /// Measure the endpoint and return the discovered budget and batch limit behavior.
        /// </summary>
        /// <param name="provisionalProfile">The profile resolved so far from options and provider defaults.</param>
        /// <param name="tokenizer">A tokenizer adapter built for the provisional profile.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A calibration result, or null when calibration could not be performed.</returns>
        Task<TokenizationCalibrationResult?> CalibrateAsync(
            ResolvedTokenizationProfile provisionalProfile,
            ITokenizerAdapter tokenizer,
            CancellationToken token = default);
    }
}
