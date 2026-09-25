namespace TextChunker.Chunkers
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using TextChunker.Chunking;
    using TextChunker.Exceptions;

    /// <summary>
    /// Splits text at boundaries defined by a user supplied regular expression, guarded by a timeout
    /// against catastrophic backtracking. Captured groups are kept as segments, as with Regex.Split.
    /// Oversized segments fall back to the token window.
    /// </summary>
    internal static class RegexChunker
    {
        internal static List<SourceSpan> Chunk(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            if (range.Length <= 0) return new List<SourceSpan>();
            if (string.IsNullOrEmpty(context.Config.RegexPattern))
                throw new InvalidChunkingOptionsException("RegexPattern is required when using the RegexBased strategy.");

            Regex regex = new Regex(
                context.Config.RegexPattern,
                RegexOptions.Compiled | RegexOptions.Multiline,
                TimeSpan.FromMilliseconds(context.Config.RegexTimeoutMilliseconds));

            List<SourceSpan> segments = ChunkingHelpers.SplitByRegex(context.Source, range, regex, true);
            if (segments.Count == 0) return ChunkingHelpers.ChunkByTokenWindow(context, range, tokenLimit);

            List<SourceSpan> chunks = new List<SourceSpan>();
            foreach (SourceSpan segment in segments)
            {
                if (context.Count(segment) <= tokenLimit)
                    chunks.Add(segment);
                else
                    chunks.AddRange(ChunkingHelpers.ChunkByTokenWindow(context, segment, tokenLimit));
            }

            return chunks;
        }
    }
}
