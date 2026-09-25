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

            // The gap is whitespace, which costs at most a token per character, so the word alone only needs a
            // recount when the gap could account for the overflow.
            int gapLength = start - gapStart;
            int standalone = gapLength == 0 || tokens - gapLength > tokenLimit ? tokens : context.Count(start, end);
            if (standalone <= tokenLimit)
            {
                units.Add(new TokenUnit(gapStart, start, end, standalone));
                return;
            }

            SplitOversized(context, units, gapStart, start, end, tokenLimit, standalone);
        }

        private static void SplitOversized(ChunkingContext context, List<TokenUnit> units, int gapStart, int start, int end, int tokenLimit, int wordTokens)
        {
            string source = context.Source;
            List<int> boundaries = GraphemeBoundaries(source, start, end);
            double elementsPerToken = (double)(boundaries.Count - 1) / Math.Max(1, wordTokens);
            int position = 0;
            bool first = true;
            int previousPiece = -1;
            int lastPiece = -1;

            while (position < boundaries.Count - 1)
            {
                int pieceStart = boundaries[position];
                int guess = position + Math.Max(1, (int)(tokenLimit * elementsPerToken * 0.97));
                int fit = LargestFit(context, pieceStart, boundaries, position + 1, boundaries.Count - 1, tokenLimit, guess, out int fitTokens);

                int pieceEnd;
                int pieceTokens;
                int nextPosition;
                if (fit < 0)
                {
                    // A single grapheme is over budget, so fall back to code point boundaries inside it. A lone code
                    // point that is still over budget is kept whole, since cutting it would corrupt the text.
                    List<int> codePoints = CodePointBoundaries(source, pieceStart, boundaries[position + 1]);
                    int codePointFit = LargestFit(context, pieceStart, codePoints, 1, codePoints.Count - 1, tokenLimit, 1, out int codePointTokens);
                    pieceEnd = codePoints[codePointFit < 0 ? 1 : codePointFit];
                    pieceTokens = codePointFit < 0 ? context.Count(pieceStart, pieceEnd) : codePointTokens;
                    nextPosition = pieceEnd == boundaries[position + 1] ? position + 1 : position;
                    if (nextPosition == position) boundaries[position] = pieceEnd;
                    previousPiece = -1;
                    lastPiece = -1;
                }
                else
                {
                    pieceEnd = boundaries[fit];
                    pieceTokens = fitTokens;
                    nextPosition = fit;
                    if (fitTokens > 0) elementsPerToken = (double)(fit - position) / fitTokens;
                    previousPiece = lastPiece;
                    lastPiece = position;
                }

                units.Add(new TokenUnit(first ? gapStart : pieceStart, pieceStart, pieceEnd, pieceTokens));
                first = false;
                position = nextPosition;
            }

            if (previousPiece >= 0 && lastPiece > previousPiece && units.Count >= 2)
                BalanceLastPieces(context, units, boundaries, previousPiece, lastPiece, tokenLimit);
        }

        private static void BalanceLastPieces(ChunkingContext context, List<TokenUnit> units, List<int> boundaries, int previousPiece, int lastPiece, int tokenLimit)
        {
            // The final piece of a split word is the remainder and may be tiny. Move the cut between the last two
            // pieces to the grapheme boundary that makes them most even, so the word does not end in a fragment.
            TokenUnit tail = units[units.Count - 1];
            if (tail.Tokens * 4 >= tokenLimit) return;

            int start = boundaries[previousPiece];
            int end = boundaries[boundaries.Count - 1];
            int low = previousPiece + 1;
            int high = lastPiece;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (context.Count(start, boundaries[middle]) >= context.Count(boundaries[middle], end)) high = middle;
                else low = middle + 1;
            }

            TokenUnit head = units[units.Count - 2];
            int best = lastPiece;
            int bestLargest = Math.Max(head.Tokens, tail.Tokens);
            int bestLeft = head.Tokens;
            int bestRight = tail.Tokens;
            for (int candidate = Math.Max(previousPiece + 1, low - 1); candidate <= Math.Min(lastPiece, low); candidate++)
            {
                int left = context.Count(start, boundaries[candidate]);
                int right = context.Count(boundaries[candidate], end);
                if (left <= tokenLimit && right <= tokenLimit && Math.Max(left, right) < bestLargest)
                {
                    best = candidate;
                    bestLargest = Math.Max(left, right);
                    bestLeft = left;
                    bestRight = right;
                }
            }

            if (best == lastPiece) return;
            units[units.Count - 2] = new TokenUnit(head.GapStart, start, boundaries[best], bestLeft);
            units[units.Count - 1] = new TokenUnit(boundaries[best], boundaries[best], end, bestRight);
        }

        private static int LargestFit(ChunkingContext context, int start, List<int> boundaries, int from, int max, int tokenLimit, int guess, out int count)
        {
            // Returns the largest boundary index in [from, max] whose prefix fits the budget, or -1 when even the
            // first boundary does not fit. The search starts at the caller's estimate of the fill point, gallops
            // toward the answer in small steps, and finishes with a binary search, so a piece costs a handful of
            // counts instead of a scan.
            count = 0;
            if (from > max) return -1;

            int candidate = Math.Max(from, Math.Min(max, guess));
            int good;
            int goodCount;
            int bad = max + 1;
            int probeCount = context.Count(start, boundaries[candidate]);
            if (probeCount <= tokenLimit)
            {
                good = candidate;
                goodCount = probeCount;
            }
            else
            {
                bad = candidate;
                if (candidate == from) return -1;

                int step = Math.Max(1, (candidate - from) / 32);
                while (true)
                {
                    int probe = Math.Max(from, bad - step);
                    probeCount = context.Count(start, boundaries[probe]);
                    if (probeCount <= tokenLimit)
                    {
                        good = probe;
                        goodCount = probeCount;
                        break;
                    }

                    bad = probe;
                    if (probe == from) return -1;
                    step *= 2;
                }
            }

            if (bad > max)
            {
                int step = Math.Max(1, (good - from + 1) / 32);
                while (good < max)
                {
                    int probe = Math.Min(max, good + step);
                    probeCount = context.Count(start, boundaries[probe]);
                    if (probeCount <= tokenLimit)
                    {
                        good = probe;
                        goodCount = probeCount;
                        step *= 2;
                    }
                    else
                    {
                        bad = probe;
                        break;
                    }
                }
            }

            int low = good + 1;
            int high = Math.Min(max, bad - 1);
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                probeCount = context.Count(start, boundaries[middle]);
                if (probeCount <= tokenLimit)
                {
                    good = middle;
                    goodCount = probeCount;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            count = goodCount;
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
