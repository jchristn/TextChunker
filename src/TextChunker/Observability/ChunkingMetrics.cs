namespace TextChunker.Observability
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Exposes the Meter and instruments used by the library so operators can watch chunk counts and chunk size
    /// distributions in production.
    /// </summary>
    public static class ChunkingMetrics
    {
        /// <summary>
        /// The name of the Meter.
        /// </summary>
        public static readonly string Name = "TextChunker";

        /// <summary>
        /// The shared Meter instance.
        /// </summary>
        public static readonly Meter Meter = new Meter(Name);

        /// <summary>
        /// Counts the total number of chunks produced.
        /// </summary>
        public static readonly Counter<long> ChunksProduced = Meter.CreateCounter<long>("textchunker.chunks_produced");

        /// <summary>
        /// Counts chunks suppressed because their source span was wholly contained in the previous chunk. The span
        /// based strategies do not produce such chunks, so a nonzero value signals a regression.
        /// </summary>
        public static readonly Counter<long> ChunksSuppressed = Meter.CreateCounter<long>("textchunker.chunks_suppressed");

        /// <summary>
        /// Records the token count of each produced chunk.
        /// </summary>
        public static readonly Histogram<int> ChunkTokenCount = Meter.CreateHistogram<int>("textchunker.chunk_tokens");
    }
}
