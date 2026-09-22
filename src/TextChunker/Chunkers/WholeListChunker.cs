namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Treats an entire list as a single chunk, splitting by line only when the whole list exceeds the budget.
    /// </summary>
    internal static class WholeListChunker
    {
        internal static List<string> Chunk(List<string> items, ChunkingConfiguration config, bool ordered, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            if (items == null || items.Count == 0) return new List<string>();

            List<string> lines = SerializeItems(items, ordered);
            string wholeList = string.Join("\n", lines);
            if (tokenizer.CountTokens(wholeList) <= tokenLimit)
                return new List<string> { wholeList };

            return ChunkingHelpers.ChunkUnits(
                lines,
                "\n",
                tokenLimit,
                tokenizer,
                0,
                item => ChunkingHelpers.ChunkByTokenSpans(item, config, tokenizer, tokenLimit));
        }

        internal static List<string> SerializeItems(List<string> items, bool ordered)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if (ordered)
                    lines.Add((i + 1) + ". " + items[i]);
                else
                    lines.Add("- " + items[i]);
            }

            return lines;
        }
    }
}
