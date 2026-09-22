namespace TextChunker.Observability
{
    using System.Diagnostics;

    /// <summary>
    /// Exposes the single ActivitySource used by the library so consumers can trace chunking operations without
    /// the library writing to the console.
    /// </summary>
    public static class ChunkingActivitySource
    {
        /// <summary>
        /// The name of the ActivitySource.
        /// </summary>
        public static readonly string Name = "TextChunker";

        /// <summary>
        /// The shared ActivitySource instance.
        /// </summary>
        public static readonly ActivitySource Source = new ActivitySource(Name);
    }
}
