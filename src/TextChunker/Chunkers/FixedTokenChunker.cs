namespace TextChunker.Chunkers
{
    using System.Collections.Generic;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Splits text into chunks of a fixed token count with optional overlap.
    /// </summary>
    internal static class FixedTokenChunker
    {
        internal static List<string> Chunk(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenLimit)
        {
            return ChunkingHelpers.ChunkByTokenSpans(text, config, tokenizer, tokenLimit);
        }
    }
}
