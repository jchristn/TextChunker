namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Splits text at paragraph boundaries (double newline), grouping paragraphs to fill a token budget.
    /// An oversized paragraph falls back to sentence chunking.
    /// </summary>
    internal static class ParagraphChunker
    {
        internal static List<string> Chunk(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            List<string> paragraphs = ChunkingHelpers.SplitParagraphs(text);
            if (paragraphs.Count == 0) return ChunkingHelpers.ChunkByTokenSpans(text, config, tokenizer, tokenLimit);

            return ChunkingHelpers.ChunkUnits(
                paragraphs,
                "\n\n",
                tokenLimit,
                tokenizer,
                ChunkingHelpers.GetUnitOverlapCount(config),
                paragraph => SentenceChunker.Chunk(paragraph, config, tokenizer, tokenLimit));
        }
    }
}
