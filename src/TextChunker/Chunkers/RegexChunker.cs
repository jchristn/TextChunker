namespace TextChunker.Chunkers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using TextChunker.Chunking;
    using TextChunker.Exceptions;
    using TextChunker.Tokenization;

    /// <summary>
    /// Splits text at boundaries defined by a user supplied regular expression, guarded by a timeout
    /// against catastrophic backtracking. Oversized segments fall back to token span chunking.
    /// </summary>
    internal static class RegexChunker
    {
        internal static List<string> Chunk(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            if (string.IsNullOrEmpty(config.RegexPattern))
                throw new InvalidChunkingOptionsException("RegexPattern is required when using the RegexBased strategy.");

            Regex regex = new Regex(
                config.RegexPattern,
                RegexOptions.Compiled | RegexOptions.Multiline,
                TimeSpan.FromMilliseconds(config.RegexTimeoutMilliseconds));

            List<string> filtered = regex.Split(text)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (filtered.Count == 0) return ChunkingHelpers.ChunkByTokenSpans(text, config, tokenizer, tokenLimit);

            List<string> chunks = new List<string>();
            foreach (string segment in filtered)
            {
                if (tokenizer.CountTokens(segment) <= tokenLimit)
                    chunks.Add(segment);
                else
                    chunks.AddRange(ChunkingHelpers.ChunkByTokenSpans(segment, config, tokenizer, tokenLimit));
            }

            return chunks;
        }
    }
}
