namespace TextChunker.Enums
{
    /// <summary>
    /// Identifies where the active tokenization profile was resolved from.
    /// </summary>
    public enum TokenizationProfileSourceEnum
    {
        /// <summary>The profile came from an explicit caller override.</summary>
        Override,
        /// <summary>The profile came from a calibration probe against a live endpoint.</summary>
        Calibration,
        /// <summary>The profile came from a provider default registry entry.</summary>
        ProviderDefault,
        /// <summary>The profile came from the global fallback default.</summary>
        GlobalFallback
    }
}
