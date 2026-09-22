namespace TextChunker.Chunkers
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Recursively splits text on a ladder of separators, descending to a finer separator only when a piece still
    /// exceeds the token budget, then merges adjacent pieces back up to the budget. This is the structure aware
    /// splitting approach used by most modern chunkers, and it produces better boundaries than a fixed window for
    /// prose, markdown, and code.
    /// </summary>
    internal static class RecursiveChunker
    {
        internal static List<string> Chunk(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            List<string> separators = config.Separators != null && config.Separators.Count > 0
                ? config.Separators
                : SeparatorSets.DefaultLadder();

            int overlapUnits = ChunkingHelpers.GetUnitOverlapCount(config);
            return SplitAndMerge(text, separators, 0, config, tokenizer, tokenLimit, overlapUnits);
        }

        private static List<string> SplitAndMerge(
            string text,
            List<string> separators,
            int startDepth,
            ChunkingConfiguration config,
            ITokenizerAdapter tokenizer,
            int tokenLimit,
            int overlapUnits)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(text)) return results;

            if (tokenizer.CountTokens(text) <= tokenLimit)
            {
                if (!string.IsNullOrWhiteSpace(text)) results.Add(text);
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

                if (text.IndexOf(candidate, StringComparison.Ordinal) >= 0)
                {
                    separator = candidate;
                    nextDepth = depth + 1;
                    break;
                }
            }

            if (separator.Length == 0)
            {
                results.AddRange(ChunkingHelpers.ChunkByTokenSpans(text, config, tokenizer, tokenLimit));
                return results;
            }

            string[] parts = text.Split(new[] { separator }, StringSplitOptions.None);
            List<string> goodSplits = new List<string>();

            foreach (string part in parts)
            {
                if (part.Length == 0) continue;

                if (tokenizer.CountTokens(part) <= tokenLimit)
                {
                    goodSplits.Add(part);
                }
                else
                {
                    if (goodSplits.Count > 0)
                    {
                        results.AddRange(Merge(goodSplits, separator, config, tokenizer, tokenLimit, overlapUnits));
                        goodSplits.Clear();
                    }

                    results.AddRange(SplitAndMerge(part, separators, nextDepth, config, tokenizer, tokenLimit, overlapUnits));
                }
            }

            if (goodSplits.Count > 0)
                results.AddRange(Merge(goodSplits, separator, config, tokenizer, tokenLimit, overlapUnits));

            return results;
        }

        private static List<string> Merge(
            List<string> units,
            string separator,
            ChunkingConfiguration config,
            ITokenizerAdapter tokenizer,
            int tokenLimit,
            int overlapUnits)
        {
            return ChunkingHelpers.ChunkUnits(
                units,
                separator,
                tokenLimit,
                tokenizer,
                overlapUnits,
                unit => ChunkingHelpers.ChunkByTokenSpans(unit, config, tokenizer, tokenLimit));
        }
    }
}
