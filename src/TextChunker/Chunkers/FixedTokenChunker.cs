namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using TextChunker.Chunking;

    /// <summary>
    /// Splits text into windows of at most a fixed token count, cut on word boundaries, with optional overlap.
    /// </summary>
    internal static class FixedTokenChunker
    {
        internal static List<SourceSpan> Chunk(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            return ChunkingHelpers.ChunkByTokenWindow(context, range, tokenLimit, false);
        }
    }
}
