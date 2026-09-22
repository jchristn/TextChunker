namespace TextChunker.Enums
{
    /// <summary>
    /// Strategy for handling chunk overlap boundaries.
    /// </summary>
    public enum OverlapStrategyEnum
    {
        /// <summary>Mechanical overlap by token count.</summary>
        SlidingWindow,
        /// <summary>Adjust overlap boundaries to the nearest sentence boundary.</summary>
        SentenceBoundaryAware,
        /// <summary>Adjust overlap boundaries to the nearest paragraph or heading boundary.</summary>
        SemanticBoundaryAware
    }
}
