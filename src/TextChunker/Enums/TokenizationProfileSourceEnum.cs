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
        /// <summary>The profile came from an exact match in the known embedding model registry. This is an
        /// authoritative configuration for a recognized model, not a guess, so it is not treated as a fallback.</summary>
        KnownModel,
        /// <summary>The profile came from a provider default chosen by API format and model name heuristics.</summary>
        ProviderDefault,
        /// <summary>The profile came from the global fallback default.</summary>
        GlobalFallback
    }
}
