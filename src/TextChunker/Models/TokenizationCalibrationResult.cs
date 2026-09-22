namespace TextChunker.Models
{
    using TextChunker.Enums;

    /// <summary>
    /// Result returned by a tokenizer calibration probe after measuring a live endpoint.
    /// </summary>
    public class TokenizationCalibrationResult
    {
        /// <summary>
        /// The effective input token budget the endpoint actually accepts. Values below 1 are ignored by the resolver.
        /// </summary>
        public int EffectiveInputBudget { get; set; } = 0;

        /// <summary>
        /// The discovered batch limit behavior for the endpoint.
        /// </summary>
        public BatchLimitModeEnum BatchLimitMode { get; set; } = BatchLimitModeEnum.Unknown;
    }
}
