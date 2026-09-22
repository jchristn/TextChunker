namespace TextChunker.Chunking
{
    /// <summary>
    /// An intermediate produced chunk before enrichment: its text, an optional breadcrumb header context, and
    /// whether the text is a literal substring of the source so that character offsets can be resolved.
    /// </summary>
    internal class RawPiece
    {
        internal string Text { get; set; } = string.Empty;

        internal string? HeaderContext { get; set; } = null;

        internal bool OffsetEligible { get; set; } = false;

        internal RawPiece(string text, string? headerContext, bool offsetEligible)
        {
            Text = text ?? string.Empty;
            HeaderContext = headerContext;
            OffsetEligible = offsetEligible;
        }
    }
}
