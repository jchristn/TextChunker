namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using TextChunker.Chunking;

    /// <summary>
    /// Splits text at sentence boundaries, grouping sentences to fill a token budget. An oversized sentence falls
    /// back to the token window.
    /// </summary>
    internal static class SentenceChunker
    {
        internal static readonly Regex SentencePattern = new Regex(@"(?<=[.!?])\s+", RegexOptions.Compiled);

        internal static List<SourceSpan> Chunk(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            if (range.Length <= 0) return new List<SourceSpan>();

            List<SourceSpan> sentences = ChunkingHelpers.SplitSentences(context, range);
            if (sentences.Count == 0) return ChunkingHelpers.ChunkByTokenWindow(context, range, tokenLimit);

            return ChunkingHelpers.PackUnits(
                context,
                sentences,
                tokenLimit,
                ChunkingHelpers.GetUnitOverlapCount(context.Config),
                sentence => ChunkingHelpers.ChunkByTokenWindow(context, sentence, tokenLimit));
        }
    }
}
