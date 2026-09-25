namespace TextChunker.Chunking
{
    /// <summary>
    /// The smallest piece the token window packs: a word (or a CJK ideograph, or a grapheme safe fragment of an
    /// oversized word) located in the source text, with an estimate of the tokens it adds to a chunk.
    /// </summary>
    internal readonly struct TokenUnit
    {
        /// <summary>
        /// Start of the whitespace gap that precedes the unit, equal to Start when there is no gap.
        /// </summary>
        internal int GapStart { get; }

        /// <summary>
        /// Inclusive start character offset of the unit.
        /// </summary>
        internal int Start { get; }

        /// <summary>
        /// Exclusive end character offset of the unit.
        /// </summary>
        internal int End { get; }

        /// <summary>
        /// Estimated tokens the unit adds to a chunk, counted together with its preceding gap.
        /// </summary>
        internal int Tokens { get; }

        /// <summary>
        /// Initialize a new unit.
        /// </summary>
        /// <param name="gapStart">Start of the preceding whitespace gap.</param>
        /// <param name="start">Inclusive start offset.</param>
        /// <param name="end">Exclusive end offset.</param>
        /// <param name="tokens">Estimated token contribution.</param>
        internal TokenUnit(int gapStart, int start, int end, int tokens)
        {
            GapStart = gapStart;
            Start = start;
            End = end;
            Tokens = tokens;
        }
    }
}
