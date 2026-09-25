namespace TextChunker.Chunking
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Splits a range of the source text into the units the token window packs: whitespace delimited words, with
    /// each CJK ideograph as its own unit because those scripts do not separate words with spaces. A word that is
    /// larger than the token budget on its own is split into fragments at grapheme cluster boundaries (or code point
    /// boundaries when a single grapheme is still too large), so a cut never lands inside a surrogate pair or
    /// between a base character and its combining marks. Every unit is located in original text coordinates.
    /// </summary>
    internal static class WordSegmenter
    {
        internal static List<TokenUnit> Segment(ChunkingContext context, SourceSpan range, int tokenLimit)
        {
            string source = context.Source;
            List<TokenUnit> units = new List<TokenUnit>();
            int gapStart = range.Start;
            int index = range.Start;

            while (index < range.End)
            {
                if (char.IsWhiteSpace(source[index]))
                {
                    index++;
                    continue;
                }

                int start = index;
                int width = CodePointWidth(source, index, range.End);
                if (IsCjkIdeograph(source, index, width))
                {
                    index += width;
                }
                else
                {
                    while (index < range.End && !char.IsWhiteSpace(source[index]))
                    {
                        int step = CodePointWidth(source, index, range.End);
                        if (index > start && IsCjkIdeograph(source, index, step)) break;
                        index += step;
                    }
                }

                AddUnit(context, units, gapStart, start, index, tokenLimit);
                gapStart = index;
            }

            return units;
        }

        internal static int CodePointWidth(string source, int index, int limit)
        {
            return char.IsHighSurrogate(source[index]) && index + 1 < limit && char.IsLowSurrogate(source[index + 1]) ? 2 : 1;
        }

        private static void AddUnit(ChunkingContext context, List<TokenUnit> units, int gapStart, int start, int end, int tokenLimit)
        {
            int tokens = context.Count(gapStart, end);
            if (tokens <= tokenLimit)
            {
                units.Add(new TokenUnit(gapStart, start, end, tokens));
                return;
            }

            int standalone = gapStart == start ? tokens : context.Count(start, end);
            if (standalone <= tokenLimit)
            {
                units.Add(new TokenUnit(gapStart, start, end, standalone));
                return;
            }

            SplitOversized(context, units, gapStart, start, end, tokenLimit);
        }

        private static void SplitOversized(ChunkingContext context, List<TokenUnit> units, int gapStart, int start, int end, int tokenLimit)
        {
            string source = context.Source;
            List<int> boundaries = GraphemeBoundaries(source, start, end);
            int position = 0;
            bool first = true;

            while (position < boundaries.Count - 1)
            {
                int pieceStart = boundaries[position];
                int fit = LargestFit(context, pieceStart, boundaries, position + 1, boundaries.Count - 1, tokenLimit, Math.Max(1, tokenLimit));

                int pieceEnd;
                int nextPosition;
                if (fit < 0)
                {
                    // A single grapheme is over budget, so fall back to code point boundaries inside it. A lone code
                    // point that is still over budget is kept whole, since cutting it would corrupt the text.
                    List<int> codePoints = CodePointBoundaries(source, pieceStart, boundaries[position + 1]);
                    int codePointFit = LargestFit(context, pieceStart, codePoints, 1, codePoints.Count - 1, tokenLimit, 1);
                    pieceEnd = codePoints[codePointFit < 0 ? 1 : codePointFit];
                    nextPosition = pieceEnd == boundaries[position + 1] ? position + 1 : position;
                    if (nextPosition == position) boundaries[position] = pieceEnd;
                }
                else
                {
                    pieceEnd = boundaries[fit];
                    nextPosition = fit;
                }

                units.Add(new TokenUnit(first ? gapStart : pieceStart, pieceStart, pieceEnd, context.Count(pieceStart, pieceEnd)));
                first = false;
                position = nextPosition;
            }
        }

        private static int LargestFit(ChunkingContext context, int start, List<int> boundaries, int from, int max, int tokenLimit, int initialStep)
        {
            if (from > max) return -1;
            if (context.Count(start, boundaries[from]) > tokenLimit) return -1;

            int good = from;
            int step = initialStep;
            while (good < max)
            {
                int candidate = Math.Min(max, good + step);
                if (context.Count(start, boundaries[candidate]) <= tokenLimit)
                {
                    good = candidate;
                    step *= 2;
                    continue;
                }

                int low = good + 1;
                int high = candidate - 1;
                while (low <= high)
                {
                    int middle = low + ((high - low) / 2);
                    if (context.Count(start, boundaries[middle]) <= tokenLimit)
                    {
                        good = middle;
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                break;
            }

            return good;
        }

        private static List<int> GraphemeBoundaries(string source, int start, int end)
        {
            int[] elementStarts = StringInfo.ParseCombiningCharacters(source.Substring(start, end - start));
            List<int> boundaries = new List<int>(elementStarts.Length + 1);
            foreach (int offset in elementStarts)
                boundaries.Add(start + offset);
            boundaries.Add(end);
            return boundaries;
        }

        private static List<int> CodePointBoundaries(string source, int start, int end)
        {
            List<int> boundaries = new List<int>();
            int index = start;
            while (index < end)
            {
                boundaries.Add(index);
                index += CodePointWidth(source, index, end);
            }

            boundaries.Add(end);
            return boundaries;
        }

        private static bool IsCjkIdeograph(string source, int index, int width)
        {
            int codePoint = width == 2 ? char.ConvertToUtf32(source[index], source[index + 1]) : source[index];
            return (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
                || (codePoint >= 0x3400 && codePoint <= 0x4DBF)
                || (codePoint >= 0x20000 && codePoint <= 0x2A6DF)
                || (codePoint >= 0x2A700 && codePoint <= 0x2CEAF)
                || (codePoint >= 0xF900 && codePoint <= 0xFAFF)
                || (codePoint >= 0x2F800 && codePoint <= 0x2FA1F);
        }
    }
}
