namespace TextChunker.Chunking
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using TextChunker.Chunkers;
    using TextChunker.Enums;
    using TextChunker.Tokenization;

    /// <summary>
    /// Shared helpers for token budget aware chunking strategies. Holds the token window (the fixed token sizing
    /// loop every strategy falls back to) and the greedy unit packer. Both work on spans of the original source text,
    /// so every chunk they produce is an exact substring with known character offsets, a cut never lands inside a
    /// word unless the word alone exceeds the budget, and a cut never splits a surrogate pair or grapheme cluster.
    /// </summary>
    internal static class ChunkingHelpers
    {
        private static readonly Regex _ParagraphBreakRegex = new Regex(@"\r\n\r\n|\n\n", RegexOptions.Compiled);

        /// <summary>
        /// Chunk a range of the source into windows of at most tokenLimit tokens that start and end on word
        /// boundaries, with the configured overlap measured in whole words.
        /// </summary>
        /// <param name="context">Chunking context.</param>
        /// <param name="range">Range of the source to chunk.</param>
        /// <param name="tokenLimit">Maximum tokens per chunk.</param>
        /// <returns>Chunk spans in source order with strictly increasing starts and ends.</returns>
        internal static List<SourceSpan> ChunkByTokenWindow(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            List<SourceSpan> spans = new List<SourceSpan>();
            if (range.Length <= 0 || tokenLimit <= 0) return spans;

            List<TokenUnit> units = WordSegmenter.Segment(context, range, tokenLimit);
            int count = units.Count;
            if (count == 0) return spans;

            int[] ends = new int[count];
            int[] estimates = new int[count];
            long totalTokens = 0;
            for (int i = 0; i < count; i++)
            {
                ends[i] = units[i].End;
                estimates[i] = units[i].Tokens;
                totalTokens += units[i].Tokens;
            }

            double averageCharsPerToken = totalTokens > 0 ? (double)range.Length / totalTokens : 1.0;
            int overlapTokens = GetOverlapTokenCount(tokenLimit, context.Config, averageCharsPerToken);

            int start = 0;
            int previousEnd = 0;
            while (start < count)
            {
                int end = FitPrefix(context, units[start].Start, ends, estimates, start, count, tokenLimit);

                // A window that adds nothing past the previous chunk would be wholly contained in it. Advance the
                // start instead of emitting it, which trades overlap for progress.
                if (end <= previousEnd && start < previousEnd)
                {
                    start++;
                    continue;
                }

                spans.Add(new SourceSpan(units[start].Start, units[end - 1].End));
                previousEnd = end;
                if (end >= count) break;

                start = NextWindowStart(context, units, start, end, overlapTokens);
            }

            return spans;
        }

        /// <summary>
        /// Greedily pack consecutive units into chunks of at most tokenLimit tokens. Each chunk spans the source from
        /// its first unit's start to its last unit's end, so the original separators between units are preserved.
        /// A unit that exceeds the budget on its own is handed to the oversized unit handler.
        /// </summary>
        /// <param name="context">Chunking context.</param>
        /// <param name="units">Trimmed, non empty unit spans in source order.</param>
        /// <param name="tokenLimit">Maximum tokens per chunk.</param>
        /// <param name="overlapUnits">Whole units repeated at the start of the next chunk.</param>
        /// <param name="oversizedUnitHandler">Splits a single unit that exceeds the budget.</param>
        /// <returns>Chunk spans in source order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when oversizedUnitHandler is null.</exception>
        internal static List<SourceSpan> PackUnits(
            ChunkingContext context,
            IReadOnlyList<SourceSpan> units,
            int tokenLimit,
            int overlapUnits,
            Func<SourceSpan, List<SourceSpan>> oversizedUnitHandler)
        {
            if (oversizedUnitHandler == null) throw new ArgumentNullException(nameof(oversizedUnitHandler));

            List<SourceSpan> spans = new List<SourceSpan>();
            if (units == null || units.Count == 0 || tokenLimit <= 0) return spans;

            int count = units.Count;
            int[] ends = new int[count];
            int[] counts = new int[count];
            for (int i = 0; i < count; i++)
            {
                ends[i] = units[i].End;
                counts[i] = context.Count(units[i]);
            }

            int index = 0;
            int previousEnd = 0;
            while (index < count)
            {
                if (counts[index] > tokenLimit)
                {
                    spans.AddRange(oversizedUnitHandler(units[index]));
                    index++;
                    previousEnd = Math.Max(previousEnd, index);
                    continue;
                }

                int packLimit = index + 1;
                while (packLimit < count && counts[packLimit] <= tokenLimit) packLimit++;

                int end = FitPrefix(context, units[index].Start, ends, counts, index, packLimit, tokenLimit);
                if (end <= previousEnd && index < previousEnd)
                {
                    index++;
                    continue;
                }

                spans.Add(new SourceSpan(units[index].Start, units[end - 1].End));
                previousEnd = end;
                if (end >= count) break;

                if (overlapUnits > 0)
                {
                    int rewind = Math.Min(overlapUnits, Math.Max(0, (end - index) - 1));
                    index = Math.Max(index + 1, end - rewind);
                }
                else
                {
                    index = end;
                }
            }

            return spans;
        }

        /// <summary>
        /// Split a range into trimmed sentence spans.
        /// </summary>
        /// <param name="context">Chunking context.</param>
        /// <param name="range">Range to split.</param>
        /// <returns>Non empty sentence spans.</returns>
        internal static List<SourceSpan> SplitSentences(ChunkingContext context, SourceSpan range)
        {
            return SplitByRegex(context.Source, range, SentenceChunker.SentencePattern, false);
        }

        /// <summary>
        /// Split a range into trimmed paragraph spans at blank lines.
        /// </summary>
        /// <param name="context">Chunking context.</param>
        /// <param name="range">Range to split.</param>
        /// <returns>Non empty paragraph spans.</returns>
        internal static List<SourceSpan> SplitParagraphs(ChunkingContext context, SourceSpan range)
        {
            return SplitByRegex(context.Source, range, _ParagraphBreakRegex, false);
        }

        /// <summary>
        /// Split a range into trimmed spans at the matches of a regular expression. When includeCaptures is true,
        /// captured groups are emitted as their own spans, mirroring Regex.Split.
        /// </summary>
        /// <param name="source">Source text.</param>
        /// <param name="range">Range to split.</param>
        /// <param name="regex">Delimiter expression.</param>
        /// <param name="includeCaptures">Whether captured groups become spans.</param>
        /// <returns>Non empty trimmed spans in source order.</returns>
        internal static List<SourceSpan> SplitByRegex(string source, SourceSpan range, Regex regex, bool includeCaptures)
        {
            List<SourceSpan> pieces = new List<SourceSpan>();
            int position = range.Start;

            Match match = regex.Match(source, range.Start, range.Length);
            while (match.Success)
            {
                if (match.Length == 0 && match.Index == position)
                {
                    match = match.NextMatch();
                    continue;
                }

                AddTrimmed(source, position, match.Index, pieces);
                if (includeCaptures)
                {
                    for (int group = 1; group < match.Groups.Count; group++)
                    {
                        Group captured = match.Groups[group];
                        if (captured.Success) AddTrimmed(source, captured.Index, captured.Index + captured.Length, pieces);
                    }
                }

                position = match.Index + match.Length;
                match = match.NextMatch();
            }

            AddTrimmed(source, position, range.End, pieces);
            return pieces;
        }

        /// <summary>
        /// Split a range into trimmed, non empty line spans.
        /// </summary>
        /// <param name="context">Chunking context.</param>
        /// <param name="range">Range to split.</param>
        /// <returns>Line spans in source order.</returns>
        internal static List<SourceSpan> SplitLines(ChunkingContext context, SourceSpan range)
        {
            List<SourceSpan> lines = new List<SourceSpan>();
            string source = context.Source;
            int position = range.Start;
            while (position <= range.End)
            {
                int newline = position < range.End ? source.IndexOf('\n', position, range.End - position) : -1;
                int lineEnd = newline < 0 ? range.End : newline;
                AddTrimmed(source, position, lineEnd, lines);
                if (newline < 0) break;
                position = newline + 1;
            }

            return lines;
        }

        /// <summary>
        /// Trim a span of leading and trailing whitespace.
        /// </summary>
        /// <param name="source">Source text.</param>
        /// <param name="span">Span to trim.</param>
        /// <returns>The trimmed span, which may be empty.</returns>
        internal static SourceSpan Trim(string source, SourceSpan span)
        {
            int start = span.Start;
            int end = span.End;
            while (start < end && char.IsWhiteSpace(source[start])) start++;
            while (end > start && char.IsWhiteSpace(source[end - 1])) end--;
            return new SourceSpan(start, end);
        }

        /// <summary>
        /// Determine whether a span holds only whitespace.
        /// </summary>
        /// <param name="source">Source text.</param>
        /// <param name="span">Span to test.</param>
        /// <returns>True when the span is empty or all whitespace.</returns>
        internal static bool IsWhiteSpace(string source, SourceSpan span)
        {
            for (int i = span.Start; i < span.End; i++)
                if (!char.IsWhiteSpace(source[i])) return false;
            return true;
        }

        /// <summary>
        /// Chunk a standalone string with the token window and return the chunk texts. Used for serialized list and
        /// table content, which has no source offsets.
        /// </summary>
        /// <param name="text">Text to chunk.</param>
        /// <param name="config">Chunking configuration.</param>
        /// <param name="tokenizer">Tokenizer.</param>
        /// <param name="tokenLimit">Maximum tokens per chunk.</param>
        /// <returns>Chunk texts.</returns>
        internal static List<string> ChunkByTokenSpans(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (string.IsNullOrEmpty(text) || tokenLimit <= 0) return new List<string>();

            ChunkingContext context = new ChunkingContext(text, config, tokenizer);
            return ChunkByTokenWindow(context, new SourceSpan(0, text.Length), tokenLimit)
                .Select(context.Text)
                .ToList();
        }

        /// <summary>
        /// Greedily pack standalone strings joined by a separator. Used for serialized list and table content, which
        /// has no source text to take spans from.
        /// </summary>
        /// <param name="units">Units to pack.</param>
        /// <param name="separator">Separator placed between units.</param>
        /// <param name="tokenLimit">Maximum tokens per chunk.</param>
        /// <param name="tokenizer">Tokenizer.</param>
        /// <param name="overlapUnits">Whole units repeated at the start of the next chunk.</param>
        /// <param name="oversizedUnitHandler">Splits a single unit that exceeds the budget.</param>
        /// <returns>Chunk texts.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tokenizer or oversizedUnitHandler is null.</exception>
        internal static List<string> ChunkUnits(
            IReadOnlyList<string> units,
            string separator,
            int tokenLimit,
            ITokenizerAdapter tokenizer,
            int overlapUnits,
            Func<string, List<string>> oversizedUnitHandler)
        {
            if (units == null || units.Count == 0 || tokenLimit <= 0) return new List<string>();
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            if (oversizedUnitHandler == null) throw new ArgumentNullException(nameof(oversizedUnitHandler));

            List<string> filtered = units.Where(unit => !string.IsNullOrWhiteSpace(unit)).ToList();
            if (filtered.Count == 0) return new List<string>();

            int count = filtered.Count;
            string[] trimmedUnits = new string[count];
            int[] unitTokenCounts = new int[count];
            for (int i = 0; i < count; i++)
            {
                trimmedUnits[i] = filtered[i].Trim();
                unitTokenCounts[i] = tokenizer.CountTokens(trimmedUnits[i]);
            }

            List<string> chunks = new List<string>();
            StringBuilder builder = new StringBuilder();
            int index = 0;
            int previousEnd = 0;

            while (index < count)
            {
                if (unitTokenCounts[index] > tokenLimit)
                {
                    List<string> oversizedChunks = oversizedUnitHandler(trimmedUnits[index]);
                    chunks.AddRange(NormalizeOversizedChunks(trimmedUnits[index], oversizedChunks, tokenLimit, tokenizer));
                    index++;
                    previousEnd = Math.Max(previousEnd, index);
                    continue;
                }

                int startIndex = index;
                builder.Clear();
                builder.Append(trimmedUnits[index]);
                index++;

                while (index < count && unitTokenCounts[index] <= tokenLimit)
                {
                    int savedLength = builder.Length;
                    builder.Append(separator);
                    builder.Append(trimmedUnits[index]);
                    if (tokenizer.CountTokens(builder.ToString()) > tokenLimit)
                    {
                        builder.Length = savedLength;
                        break;
                    }

                    index++;
                }

                if (index <= previousEnd && startIndex < previousEnd)
                {
                    index = startIndex + 1;
                    continue;
                }

                chunks.Add(builder.ToString());
                previousEnd = index;
                if (overlapUnits > 0 && index < count)
                {
                    int rewind = Math.Min(overlapUnits, Math.Max(0, (index - startIndex) - 1));
                    index = Math.Max(startIndex + 1, index - rewind);
                }
            }

            return chunks;
        }

        internal static int GetUnitOverlapCount(ChunkingConfiguration config)
        {
            if (config.OverlapPercentage.HasValue)
                return config.OverlapPercentage.Value > 0 ? 1 : 0;

            if (config.OverlapCharacters.HasValue)
                return config.OverlapCharacters.Value > 0 ? 1 : 0;

            return Math.Max(0, config.OverlapCount);
        }

        private static int FitPrefix(ChunkingContext context, int startOffset, int[] ends, int[] estimates, int first, int max, int tokenLimit)
        {
            // Returns the exclusive index of the last unit that still fits. The per unit estimates pick a first
            // guess; the actual count of the candidate text then decides, galloping and binary searching from the
            // guess so that a chunk costs a logarithmic number of counts even when the estimates are off.
            int end = first;
            int estimate = 0;
            while (end < max && (end == first || estimate + estimates[end] <= tokenLimit))
            {
                estimate += estimates[end];
                end++;
            }

            int actual = context.Count(startOffset, ends[end - 1]);
            if (actual > tokenLimit)
            {
                int bad = end;
                int good = first + 1;
                int step = 1;
                while (bad - step > first + 1)
                {
                    int candidate = bad - step;
                    if (context.Count(startOffset, ends[candidate - 1]) <= tokenLimit)
                    {
                        good = candidate;
                        break;
                    }

                    bad = candidate;
                    step *= 2;
                }

                return BinarySearchFit(context, startOffset, ends, good, bad, tokenLimit);
            }

            if (end >= max || actual + estimates[end] > tokenLimit) return end;

            int fit = end;
            int growth = 1;
            while (fit < max)
            {
                int candidate = Math.Min(max, fit + growth);
                if (context.Count(startOffset, ends[candidate - 1]) <= tokenLimit)
                {
                    fit = candidate;
                    growth *= 2;
                    continue;
                }

                return BinarySearchFit(context, startOffset, ends, fit, candidate, tokenLimit);
            }

            return fit;
        }

        private static int BinarySearchFit(ChunkingContext context, int startOffset, int[] ends, int good, int bad, int tokenLimit)
        {
            int low = good + 1;
            int high = bad - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (context.Count(startOffset, ends[middle - 1]) <= tokenLimit)
                {
                    good = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return good;
        }

        private static int NextWindowStart(ChunkingContext context, List<TokenUnit> units, int start, int end, int overlapTokens)
        {
            if (overlapTokens <= 0) return end;

            int next = end;
            int accumulated = 0;
            while (next - 1 > start && accumulated + units[next - 1].Tokens <= overlapTokens)
            {
                accumulated += units[next - 1].Tokens;
                next--;
            }

            switch (context.Config.OverlapStrategy)
            {
                case OverlapStrategyEnum.SentenceBoundaryAware:
                    return SnapToBoundary(context, units, start, end, overlapTokens, next, true);
                case OverlapStrategyEnum.SemanticBoundaryAware:
                    return SnapToBoundary(context, units, start, end, overlapTokens, next, false);
                default:
                    return next;
            }
        }

        private static int SnapToBoundary(ChunkingContext context, List<TokenUnit> units, int start, int end, int overlapTokens, int fallback, bool sentence)
        {
            // Consider every boundary inside the chunk (a unit that begins a sentence, or a unit that begins a
            // paragraph) and choose the one whose overlap is closest to the requested overlap, preferring more
            // context on a tie. The candidate at end means no overlap, starting cleanly at the next boundary.
            int best = -1;
            int bestDistance = int.MaxValue;
            int overlap = 0;
            for (int candidate = end; candidate > start; candidate--)
            {
                if (candidate < end) overlap += units[candidate].Tokens;

                bool isBoundary = sentence
                    ? EndsSentence(context.Source, units[candidate - 1])
                    : StartsParagraph(context.Source, units[candidate]);
                if (!isBoundary) continue;

                int distance = Math.Abs(overlap - overlapTokens);
                if (distance <= bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best > start ? best : fallback;
        }

        private static bool EndsSentence(string source, TokenUnit unit)
        {
            int index = unit.End - 1;
            while (index > unit.Start && IsClosingMark(source[index])) index--;
            char last = source[index];
            return last == '.' || last == '!' || last == '?' || last == '\u3002' || last == '\uFF01' || last == '\uFF1F';
        }

        private static bool IsClosingMark(char value)
        {
            return value == '"' || value == '\'' || value == ')' || value == ']' || value == '}'
                || value == '\u201D' || value == '\u2019' || value == '\u00BB';
        }

        private static bool StartsParagraph(string source, TokenUnit unit)
        {
            int newlines = 0;
            for (int i = unit.GapStart; i < unit.Start; i++)
            {
                if (source[i] == '\n' && ++newlines >= 2) return true;
            }

            return false;
        }

        private static void AddTrimmed(string source, int start, int end, List<SourceSpan> pieces)
        {
            SourceSpan trimmed = Trim(source, new SourceSpan(start, end));
            if (trimmed.Length > 0) pieces.Add(trimmed);
        }

        private static List<string> NormalizeOversizedChunks(
            string originalUnit,
            List<string> candidateChunks,
            int tokenLimit,
            ITokenizerAdapter tokenizer)
        {
            if (candidateChunks == null || candidateChunks.Count == 0)
                return ChunkByTokenSpans(originalUnit, new ChunkingConfiguration(), tokenizer, tokenLimit);

            List<string> normalized = new List<string>();
            foreach (string candidate in candidateChunks.Where(chunk => !string.IsNullOrWhiteSpace(chunk)))
            {
                if (tokenizer.CountTokens(candidate) <= tokenLimit)
                    normalized.Add(candidate);
                else
                    normalized.AddRange(ChunkByTokenSpans(candidate, new ChunkingConfiguration(), tokenizer, tokenLimit));
            }

            if (normalized.Count == 0)
                normalized.AddRange(ChunkByTokenSpans(originalUnit, new ChunkingConfiguration(), tokenizer, tokenLimit));

            return normalized;
        }

        private static int GetOverlapTokenCount(int chunkSize, ChunkingConfiguration config, double averageCharsPerToken)
        {
            if (config.OverlapPercentage.HasValue)
                return (int)(chunkSize * config.OverlapPercentage.Value);

            if (config.OverlapCharacters.HasValue)
            {
                double average = averageCharsPerToken <= 0 ? 1.0 : averageCharsPerToken;
                return Math.Max(0, (int)Math.Round(config.OverlapCharacters.Value / average));
            }

            return config.OverlapCount;
        }
    }
}
