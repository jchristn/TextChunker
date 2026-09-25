namespace TextChunker.Chunkers
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Chunking;

    /// <summary>
    /// Recursively splits text on a ladder of separators, descending to a finer separator only when a piece still
    /// exceeds the token budget, then merges adjacent pieces back up to the budget. This is the structure aware
    /// splitting approach used by most modern chunkers, and it produces better boundaries than a fixed window for
    /// prose, markdown, and code.
    /// </summary>
    internal static class RecursiveChunker
    {
        internal static List<SourceSpan> Chunk(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            if (range.Length <= 0) return new List<SourceSpan>();

            List<string> separators = context.Config.Separators != null && context.Config.Separators.Count > 0
                ? context.Config.Separators
                : SeparatorSets.DefaultLadder();

            int overlapUnits = ChunkingHelpers.GetUnitOverlapCount(context.Config);
            return SplitAndMerge(context, range, separators, 0, tokenLimit, overlapUnits);
        }

        private static List<SourceSpan> SplitAndMerge(
            ChunkingContext context,
            SourceSpan span,
            List<string> separators,
            int startDepth,
            int tokenLimit,
            int overlapUnits)
        {
            List<SourceSpan> results = new List<SourceSpan>();
            if (span.Length <= 0) return results;

            string source = context.Source;
            if (context.Count(span) <= tokenLimit)
            {
                if (!ChunkingHelpers.IsWhiteSpace(source, span)) results.Add(span);
                return results;
            }

            string separator = string.Empty;
            int nextDepth = separators.Count;
            for (int depth = startDepth; depth < separators.Count; depth++)
            {
                string candidate = separators[depth];
                if (candidate.Length == 0)
                {
                    separator = string.Empty;
                    nextDepth = depth + 1;
                    break;
                }

                if (source.IndexOf(candidate, span.Start, span.Length, StringComparison.Ordinal) >= 0)
                {
                    separator = candidate;
                    nextDepth = depth + 1;
                    break;
                }
            }

            if (separator.Length == 0)
            {
                results.AddRange(ChunkingHelpers.ChunkByTokenWindow(context, span, tokenLimit));
                return results;
            }

            List<SourceSpan> goodSplits = new List<SourceSpan>();
            foreach (SourceSpan part in SplitOn(source, span, separator))
            {
                if (context.Count(part) <= tokenLimit)
                {
                    goodSplits.Add(part);
                }
                else
                {
                    if (goodSplits.Count > 0)
                    {
                        results.AddRange(Merge(context, goodSplits, tokenLimit, overlapUnits));
                        goodSplits.Clear();
                    }

                    results.AddRange(SplitAndMerge(context, part, separators, nextDepth, tokenLimit, overlapUnits));
                }
            }

            if (goodSplits.Count > 0)
                results.AddRange(Merge(context, goodSplits, tokenLimit, overlapUnits));

            return results;
        }

        private static List<SourceSpan> SplitOn(string source, SourceSpan span, string separator)
        {
            // Only the whitespace of a separator is discarded. Its visible core stays with the text it belongs to:
            // a separator that opens a block after a line break ("\n## ", "\nclass ") keeps its core with the part
            // that follows, and any other separator (". ") keeps its core with the part that precedes it. That way a
            // chunk boundary never drops a heading marker, a keyword, or sentence punctuation.
            int leading = 0;
            while (leading < separator.Length && char.IsWhiteSpace(separator[leading])) leading++;
            int trailing = 0;
            while (trailing < separator.Length - leading && char.IsWhiteSpace(separator[separator.Length - 1 - trailing])) trailing++;
            int coreLength = separator.Length - leading - trailing;
            bool coreOpensNextPart = coreLength > 0 && leading > 0;
            bool coreClosesPreviousPart = coreLength > 0 && leading == 0;

            List<SourceSpan> parts = new List<SourceSpan>();
            int position = span.Start;
            while (position <= span.End)
            {
                int found = position < span.End
                    ? source.IndexOf(separator, position, span.End - position, StringComparison.Ordinal)
                    : -1;
                int partEnd = found < 0 ? span.End : (coreClosesPreviousPart ? found + coreLength : found);
                if (partEnd > position) parts.Add(new SourceSpan(position, partEnd));
                if (found < 0) break;
                position = coreOpensNextPart ? found + leading : found + separator.Length;
            }

            return parts;
        }

        private static List<SourceSpan> Merge(ChunkingContext context, List<SourceSpan> parts, int tokenLimit, int overlapUnits)
        {
            List<SourceSpan> units = new List<SourceSpan>(parts.Count);
            foreach (SourceSpan part in parts)
            {
                SourceSpan trimmed = ChunkingHelpers.Trim(context.Source, part);
                if (trimmed.Length > 0) units.Add(trimmed);
            }

            return ChunkingHelpers.PackUnits(
                context,
                units,
                tokenLimit,
                overlapUnits,
                unit => ChunkingHelpers.ChunkByTokenWindow(context, unit, tokenLimit));
        }
    }
}
