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
    /// Shared helpers for token budget aware chunking strategies. Holds the strict token span sizing loop
    /// and the greedy unit packer that every strategy ultimately relies on.
    /// </summary>
    internal static class ChunkingHelpers
    {
        private static readonly Regex _SentenceBoundaryRegex = new Regex(@"[.!?][\s]", RegexOptions.Compiled | RegexOptions.RightToLeft);

        internal static List<string> ChunkByTokenSpans(
            string text,
            ChunkingConfiguration config,
            ITokenizerAdapter tokenizer,
            int tokenLimit)
        {
            if (string.IsNullOrEmpty(text) || tokenLimit <= 0) return new List<string>();

            int totalTokens = tokenizer.CountTokens(text);
            if (totalTokens <= 0) return new List<string>();

            double averageCharsPerToken = (double)text.Length / totalTokens;
            int overlapTokens = GetOverlapTokenCount(tokenLimit, config, averageCharsPerToken);
            List<string> chunks = new List<string>();
            int position = 0;

            while (position < totalTokens)
            {
                int requestedTokenCount = Math.Min(tokenLimit, totalTokens - position);
                TokenSlice slice = CreateStrictTokenSlice(text, position, requestedTokenCount, tokenizer, tokenLimit);
                if (slice.TokenCount <= 0) break;

                if (!string.IsNullOrWhiteSpace(slice.Text))
                    chunks.Add(slice.Text);

                if (position + slice.TokenCount >= totalTokens) break;

                int advance = slice.TokenCount - overlapTokens;
                if (advance <= 0) advance = 1;

                if (config.OverlapStrategy == OverlapStrategyEnum.SentenceBoundaryAware && overlapTokens > 0)
                {
                    int adjusted = AdjustToSentenceBoundary(tokenizer, text, Math.Min(position + advance, totalTokens));
                    position = adjusted > position ? adjusted : position + advance;
                }
                else if (config.OverlapStrategy == OverlapStrategyEnum.SemanticBoundaryAware && overlapTokens > 0)
                {
                    int adjusted = AdjustToParagraphBoundary(tokenizer, text, Math.Min(position + advance, totalTokens));
                    position = adjusted > position ? adjusted : position + advance;
                }
                else
                {
                    position += advance;
                }
            }

            return chunks;
        }

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

            // Pre-trim and pre-count each unit once. This produces identical decisions to counting on demand
            // while avoiding repeated work when overlap rewinds revisit a unit. The greedy candidate is built with
            // a StringBuilder rather than re-joining the whole accumulated list on every unit, which removes the
            // quadratic string building that dominated large inputs.
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

            while (index < count)
            {
                int startIndex = index;
                int currentUnitCount = 0;
                builder.Clear();

                while (index < count)
                {
                    string unit = trimmedUnits[index];

                    if (unitTokenCounts[index] > tokenLimit)
                    {
                        if (currentUnitCount > 0) break;

                        List<string> oversizedChunks = oversizedUnitHandler(unit);
                        oversizedChunks = NormalizeOversizedChunks(unit, oversizedChunks, tokenLimit, tokenizer);
                        chunks.AddRange(oversizedChunks);
                        index++;
                        startIndex = index;
                        continue;
                    }

                    if (currentUnitCount == 0)
                    {
                        builder.Append(unit);
                        currentUnitCount++;
                        index++;
                    }
                    else
                    {
                        int savedLength = builder.Length;
                        builder.Append(separator);
                        builder.Append(unit);
                        if (tokenizer.CountTokens(builder.ToString()) > tokenLimit)
                        {
                            builder.Length = savedLength;
                            break;
                        }

                        currentUnitCount++;
                        index++;
                    }
                }

                if (currentUnitCount > 0)
                {
                    chunks.Add(builder.ToString());
                    if (overlapUnits > 0 && index < count)
                    {
                        int rewind = Math.Min(overlapUnits, Math.Max(0, currentUnitCount - 1));
                        index = Math.Max(startIndex + 1, index - rewind);
                    }
                }
            }

            return chunks;
        }

        internal static List<string> SplitParagraphs(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            return text
                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(paragraph => paragraph.Trim())
                .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
                .ToList();
        }

        internal static List<string> SplitSentences(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            return SentenceChunker.SentencePattern
                .Split(text)
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .Select(sentence => sentence.Trim())
                .ToList();
        }

        internal static int GetUnitOverlapCount(ChunkingConfiguration config)
        {
            if (config.OverlapPercentage.HasValue)
                return config.OverlapPercentage.Value > 0 ? 1 : 0;

            if (config.OverlapCharacters.HasValue)
                return config.OverlapCharacters.Value > 0 ? 1 : 0;

            return Math.Max(0, config.OverlapCount);
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
                {
                    normalized.Add(candidate);
                }
                else
                {
                    normalized.AddRange(ChunkByTokenSpans(candidate, new ChunkingConfiguration(), tokenizer, tokenLimit));
                }
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

        private static TokenSlice CreateStrictTokenSlice(
            string text,
            int startTokenIndex,
            int requestedTokenCount,
            ITokenizerAdapter tokenizer,
            int tokenLimit)
        {
            if (requestedTokenCount <= 0) return new TokenSlice(string.Empty, 0);

            int candidateTokenCount = requestedTokenCount;
            while (candidateTokenCount > 0)
            {
                string chunkText = tokenizer.SliceByTokenRange(text, startTokenIndex, candidateTokenCount);
                if (string.IsNullOrWhiteSpace(chunkText))
                {
                    candidateTokenCount--;
                    continue;
                }

                int actualTokenCount = tokenizer.CountTokens(chunkText);
                if (actualTokenCount > 0 && actualTokenCount <= tokenLimit)
                    return new TokenSlice(chunkText, actualTokenCount);

                candidateTokenCount = actualTokenCount > 0
                    ? Math.Min(candidateTokenCount - 1, actualTokenCount - 1)
                    : candidateTokenCount - 1;
            }

            return new TokenSlice(string.Empty, 0);
        }

        private static int AdjustToSentenceBoundary(ITokenizerAdapter tokenizer, string text, int tokenPosition)
        {
            string decodedUpToPos = tokenizer.SliceByTokenRange(text, 0, tokenPosition);
            Match match = _SentenceBoundaryRegex.Match(decodedUpToPos);
            if (match.Success)
            {
                string upToSentence = decodedUpToPos.Substring(0, match.Index + 1);
                return tokenizer.CountTokens(upToSentence);
            }

            return tokenPosition;
        }

        private static int AdjustToParagraphBoundary(ITokenizerAdapter tokenizer, string text, int tokenPosition)
        {
            string decodedUpToPos = tokenizer.SliceByTokenRange(text, 0, tokenPosition);
            int lastParagraphIndex = decodedUpToPos.LastIndexOf("\n\n", StringComparison.Ordinal);
            if (lastParagraphIndex > 0)
            {
                string upToParagraph = decodedUpToPos.Substring(0, lastParagraphIndex + 2);
                return tokenizer.CountTokens(upToParagraph);
            }

            return tokenPosition;
        }
    }
}
