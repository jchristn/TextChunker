namespace TextChunker.Enums
{
    /// <summary>
    /// Controls how chunks smaller than the configured minimum token count are handled.
    /// </summary>
    public enum SmallChunkModeEnum
    {
        /// <summary>Keep small chunks exactly as produced. This is the default.</summary>
        Keep,
        /// <summary>Merge a small chunk into the following chunk where possible.</summary>
        MergeForward,
        /// <summary>Drop small chunks entirely.</summary>
        Drop
    }
}
