namespace TextChunker.Chunking
{
    /// <summary>
    /// An intermediate produced chunk before enrichment: its text, an optional breadcrumb header context, and, when
    /// the text is an exact substring of the source, its character offsets in that source.
    /// </summary>
    internal class RawPiece
    {
        internal string Text { get; set; } = string.Empty;

        internal string? HeaderContext { get; set; } = null;

        internal int StartOffset { get; set; } = -1;

        internal int EndOffset { get; set; } = -1;

        internal bool HasOffsets => StartOffset >= 0 && EndOffset >= StartOffset;

        internal RawPiece(string text, string? headerContext)
        {
            Text = text ?? string.Empty;
            HeaderContext = headerContext;
        }

        internal RawPiece(string text, string? headerContext, int startOffset, int endOffset)
        {
            Text = text ?? string.Empty;
            HeaderContext = headerContext;
            StartOffset = startOffset;
            EndOffset = endOffset;
        }
    }
}
