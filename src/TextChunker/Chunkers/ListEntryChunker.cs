namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using System.Linq;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Each list item becomes its own chunk. Oversized items fall back to token span chunking.
    /// </summary>
    internal static class ListEntryChunker
    {
        internal static List<string> Chunk(List<string> items, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (items == null || items.Count == 0) return new List<string>();

            List<string> chunks = new List<string>();
            foreach (string item in items.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                if (tokenizer.CountTokens(item) <= tokenLimit)
                    chunks.Add(item);
                else
                    chunks.AddRange(ChunkingHelpers.ChunkByTokenSpans(item, config, tokenizer, tokenLimit));
            }

            return chunks;
        }
    }
}
