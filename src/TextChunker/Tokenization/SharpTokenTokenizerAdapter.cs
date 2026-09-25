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

        /// <summary>
        /// Return the span of the original text covered by a range of tokens. Token boundaries are mapped back to
        /// character positions, so the result is always a substring of the input: a range that starts or ends inside a
        /// multi byte character (a byte level token that holds part of an emoji, for example) is widened at the start
        /// and narrowed at the end to the character boundary, and never yields U+FFFD or a lone surrogate.
        /// </summary>
        /// <param name="text">Source text.</param>
        /// <param name="startTokenIndex">Zero based token index to start from. Must be at least 0.</param>
        /// <param name="tokenCount">Number of tokens to include. Values of 0 or less return an empty string.</param>
        /// <returns>The original text covered by the token range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when startTokenIndex is negative.</exception>
        public string SliceByTokenRange(string text, int startTokenIndex, int tokenCount)
        {
            if (startTokenIndex < 0) throw new ArgumentOutOfRangeException(nameof(startTokenIndex));
            if (string.IsNullOrEmpty(text) || tokenCount <= 0) return string.Empty;

            List<int> tokens = _Encoding.Encode(text);
            if (startTokenIndex >= tokens.Count) return string.Empty;

            int count = Math.Min(tokenCount, tokens.Count - startTokenIndex);
            if (count <= 0) return string.Empty;

            int[]? boundaries = MapTokenBoundaries(text, tokens);
            if (boundaries == null)
                return _Encoding.Decode(tokens.GetRange(startTokenIndex, count));

            int start = boundaries[startTokenIndex];
            int end = Math.Max(start, boundaries[startTokenIndex + count]);
            return text.Substring(start, end - start);
        }

        private int[]? MapTokenBoundaries(string text, List<int> tokens)
        {
            // boundaries[i] is the character offset where token i begins. Tokens that together encode one character
            // share the offset of that character's start, so a cut between them snaps to the character boundary.
            int[] boundaries = new int[tokens.Count + 1];
            List<int> pending = new List<int>(4);
            int groupStart = 0;
            int position = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (pending.Count == 0) groupStart = i;
                pending.Add(tokens[i]);

                string decoded = _Encoding.Decode(pending);
                bool matches = position + decoded.Length <= text.Length
                    && string.CompareOrdinal(text, position, decoded, 0, decoded.Length) == 0;
                if (!matches)
                {
                    if (pending.Count >= 8) return null;
                    continue;
                }

                for (int j = groupStart; j <= i; j++) boundaries[j] = position;
                position += decoded.Length;
                pending.Clear();
            }

            if (pending.Count > 0 || position != text.Length) return null;
            boundaries[tokens.Count] = position;
            return boundaries;
        }
    }
}
