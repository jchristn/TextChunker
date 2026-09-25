namespace TextChunker.Chunking
{
    /// <summary>
    /// A half open character range [Start, End) within the source text being chunked.
    /// </summary>
    internal readonly struct SourceSpan
    {
        /// <summary>
        /// Inclusive start character offset.
        /// </summary>
        internal int Start { get; }

        /// <summary>
        /// Exclusive end character offset.
        /// </summary>
        internal int End { get; }

        /// <summary>
        /// Number of characters in the span.
        /// </summary>
        internal int Length => End - Start;

        /// <summary>
        /// Initialize a new span.
        /// </summary>
        /// <param name="start">Inclusive start offset.</param>
        /// <param name="end">Exclusive end offset.</param>
        internal SourceSpan(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}
