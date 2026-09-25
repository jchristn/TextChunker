namespace TextChunker.Tokenization
{
    /// <summary>
    /// A WordPiece token with its vocabulary identifier and its character range in the original, unnormalized text.
    /// </summary>
    internal readonly struct WordPieceToken
    {
        /// <summary>
        /// Vocabulary identifier.
        /// </summary>
        internal int Id { get; }

        /// <summary>
        /// Inclusive start character offset in the original text.
        /// </summary>
        internal int Start { get; }

        /// <summary>
        /// Exclusive end character offset in the original text.
        /// </summary>
        internal int End { get; }

        /// <summary>
        /// Initialize a new token.
        /// </summary>
        /// <param name="id">Vocabulary identifier.</param>
        /// <param name="start">Inclusive start offset in the original text.</param>
        /// <param name="end">Exclusive end offset in the original text.</param>
        internal WordPieceToken(int id, int start, int end)
        {
            Id = id;
            Start = start;
            End = end;
        }
    }
}
