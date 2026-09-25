namespace TextChunker.Chunking
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Tokenization;

    /// <summary>
    /// Per operation state shared by the span based strategies: the source text every span refers to, the
    /// configuration, the tokenizer, and a cache of token counts for short strings, since words and short units
    /// repeat heavily within a document. Not thread safe; one instance serves one chunking operation.
    /// </summary>
    internal sealed class ChunkingContext
    {
        private const int _CacheableLength = 64;
        private const int _MaxCacheEntries = 200_000;

        private readonly Dictionary<string, int> _Cache = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Source text that every span indexes into.
        /// </summary>
        internal string Source { get; }

        /// <summary>
        /// Chunking configuration.
        /// </summary>
        internal ChunkingConfiguration Config { get; }

        /// <summary>
        /// Tokenizer used for every count.
        /// </summary>
        internal ITokenizerAdapter Tokenizer { get; }

        /// <summary>
        /// Initialize a new context.
        /// </summary>
        /// <param name="source">Source text.</param>
        /// <param name="config">Chunking configuration.</param>
        /// <param name="tokenizer">Tokenizer.</param>
        /// <exception cref="ArgumentNullException">Thrown when config or tokenizer is null.</exception>
        internal ChunkingContext(string source, ChunkingConfiguration config, ITokenizerAdapter tokenizer)
        {
            Source = source ?? string.Empty;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        /// <summary>
        /// Count the tokens in a range of the source text.
        /// </summary>
        /// <param name="start">Inclusive start offset.</param>
        /// <param name="end">Exclusive end offset.</param>
        /// <returns>Token count.</returns>
        internal int Count(int start, int end)
        {
            if (end <= start) return 0;
            return Count(Source.Substring(start, end - start));
        }

        /// <summary>
        /// Count the tokens in a span of the source text.
        /// </summary>
        /// <param name="span">Span to count.</param>
        /// <returns>Token count.</returns>
        internal int Count(SourceSpan span)
        {
            return Count(span.Start, span.End);
        }

        /// <summary>
        /// Count the tokens in a string, caching the result for short strings.
        /// </summary>
        /// <param name="text">Text to count.</param>
        /// <returns>Token count.</returns>
        internal int Count(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (text.Length > _CacheableLength) return Tokenizer.CountTokens(text);

            if (_Cache.TryGetValue(text, out int cached)) return cached;

            int count = Tokenizer.CountTokens(text);
            if (_Cache.Count < _MaxCacheEntries) _Cache[text] = count;
            return count;
        }

        /// <summary>
        /// Return the text of a span.
        /// </summary>
        /// <param name="span">Span.</param>
        /// <returns>The substring of the source covered by the span.</returns>
        internal string Text(SourceSpan span)
        {
            return Source.Substring(span.Start, span.Length);
        }
    }
}
