namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using TextChunker.Chunking;

    /// <summary>
    /// Splits text at paragraph boundaries (blank lines), grouping paragraphs to fill a token budget.
    /// An oversized paragraph falls back to sentence chunking.
    /// </summary>
    internal static class ParagraphChunker
    {
        internal static List<SourceSpan> Chunk(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            if (range.Length <= 0) return new List<SourceSpan>();

            List<SourceSpan> paragraphs = ChunkingHelpers.SplitParagraphs(context, range);
            if (paragraphs.Count == 0) return ChunkingHelpers.ChunkByTokenWindow(context, range, tokenLimit);

            return ChunkingHelpers.PackUnits(
                context,
                paragraphs,
                tokenLimit,
                ChunkingHelpers.GetUnitOverlapCount(context.Config),
                paragraph => SentenceChunker.Chunk(context, paragraph, tokenLimit));
        }
    }
}
