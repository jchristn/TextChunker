namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Splits text at sentence boundaries, grouping sentences to fill a token budget.
    /// </summary>
    internal static class SentenceChunker
    {
        internal static readonly Regex SentencePattern = new Regex(@"(?<=[.!?])\s+", RegexOptions.Compiled);

        internal static List<string> Chunk(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            List<string> sentences = ChunkingHelpers.SplitSentences(text);
            if (sentences.Count == 0) return ChunkingHelpers.ChunkByTokenSpans(text, config, tokenizer, tokenLimit);

            return ChunkingHelpers.ChunkUnits(
                sentences,
                " ",
                tokenLimit,
                tokenizer,
                ChunkingHelpers.GetUnitOverlapCount(config),
                sentence => ChunkingHelpers.ChunkByTokenSpans(sentence, config, tokenizer, tokenLimit));
        }
    }
}
