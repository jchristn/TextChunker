namespace TextChunker.Tokenization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpToken;

    /// <summary>
    /// Tokenizer adapter backed by SharpToken, providing OpenAI style BPE tokenization such as cl100k_base.
    /// </summary>
    public class SharpTokenTokenizerAdapter : ITokenizerAdapter
    {
        private readonly GptEncoding _Encoding;

        /// <summary>
        /// Initialize a new SharpToken backed adapter for the supplied encoding.
        /// </summary>
        /// <param name="encodingName">SharpToken encoding name, for example cl100k_base.</param>
        /// <exception cref="ArgumentNullException">Thrown when encodingName is null or whitespace.</exception>
        public SharpTokenTokenizerAdapter(string encodingName)
        {
            if (string.IsNullOrWhiteSpace(encodingName)) throw new ArgumentNullException(nameof(encodingName));
            _Encoding = GptEncoding.GetEncoding(encodingName);
        }

        /// <inheritdoc />
        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return _Encoding.Encode(text).Count;
        }

        /// <inheritdoc />
        public IReadOnlyList<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
            return _Encoding.Encode(text);
        }

        /// <inheritdoc />
        public string Decode(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            return _Encoding.Decode(tokenIds.ToList());
        }

        /// <inheritdoc />
        public string SliceByTokenRange(string text, int startTokenIndex, int tokenCount)
        {
            if (string.IsNullOrEmpty(text) || tokenCount <= 0) return string.Empty;
            if (startTokenIndex < 0) throw new ArgumentOutOfRangeException(nameof(startTokenIndex));

            List<int> tokens = _Encoding.Encode(text);
            if (startTokenIndex >= tokens.Count) return string.Empty;

            int count = Math.Min(tokenCount, tokens.Count - startTokenIndex);
            if (count <= 0) return string.Empty;

            return _Encoding.Decode(tokens.GetRange(startTokenIndex, count));
        }
    }
}
