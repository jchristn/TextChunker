namespace TextChunker.Chunking
{
    /// <summary>
    /// An immutable pairing of a decoded chunk text and its verified token count, used by the sizing loop.
    /// </summary>
    internal readonly struct TokenSlice
    {
        /// <summary>
        /// The decoded chunk text.
        /// </summary>
        internal string Text { get; }

        /// <summary>
        /// The verified token count of the decoded text.
        /// </summary>
        internal int TokenCount { get; }

        /// <summary>
        /// Initialize a new token slice.
        /// </summary>
        /// <param name="text">Decoded chunk text.</param>
        /// <param name="tokenCount">Verified token count.</param>
        internal TokenSlice(string text, int tokenCount)
        {
            Text = text;
            TokenCount = tokenCount;
        }
    }
}
