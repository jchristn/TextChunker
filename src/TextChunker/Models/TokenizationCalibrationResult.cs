namespace TextChunker.Models
{
    /// <summary>
    /// Result returned by a tokenizer calibration probe after measuring a live endpoint.
    /// </summary>
    public class TokenizationCalibrationResult
    {
        /// <summary>
        /// The effective input token budget the endpoint actually accepts. Values below 1 are ignored by the resolver.
        /// </summary>
        public int EffectiveInputBudget { get; set; } = 0;
    }
}
