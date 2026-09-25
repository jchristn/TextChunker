namespace TextChunker.Tokenization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.ML.Tokenizers;

    /// <summary>
    /// Tokenizer adapter that wraps any Microsoft.ML.Tokenizers tokenizer. Use this to plug in a Hugging Face
    /// tokenizer, a SentencePiece model (Llama, Gemma, T5), or any other tokenizer that the ML.Tokenizers library
    /// can construct, so token counts match the exact model you target.
    /// </summary>
    public class MlTokenizerAdapter : ITokenizerAdapter
    {
        private readonly Tokenizer _Tokenizer;

        /// <summary>
        /// Initialize a new adapter around a constructed ML.Tokenizers tokenizer.
        /// </summary>
        /// <param name="tokenizer">The tokenizer to wrap.</param>
        /// <exception cref="ArgumentNullException">Thrown when tokenizer is null.</exception>
        public MlTokenizerAdapter(Tokenizer tokenizer)
        {
            _Tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        /// <inheritdoc />
        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return _Tokenizer.CountTokens(text);
        }

        /// <inheritdoc />
        public IReadOnlyList<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
            return _Tokenizer.EncodeToIds(text);
        }

        /// <inheritdoc />
        public string Decode(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            return _Tokenizer.Decode(tokenIds.ToArray());
        }

        /// <summary>
        /// Return the text covered by a range of tokens. When the wrapped tokenizer does not normalize its input, token
        /// offsets index the original text and the result is an exact substring, widened or narrowed so it never
        /// splits a surrogate pair. When it does normalize, offsets refer to the normalized text, so the range is
        /// decoded instead and any U+FFFD produced by a partial character at either edge is removed.
        /// </summary>
        /// <param name="text">Source text.</param>
        /// <param name="startTokenIndex">Zero based token index to start from. Must be at least 0.</param>
        /// <param name="tokenCount">Number of tokens to include. Values of 0 or less return an empty string.</param>
        /// <returns>Text for the requested token range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when startTokenIndex is negative.</exception>
        public string SliceByTokenRange(string text, int startTokenIndex, int tokenCount)
        {
            if (startTokenIndex < 0) throw new ArgumentOutOfRangeException(nameof(startTokenIndex));
            if (string.IsNullOrEmpty(text) || tokenCount <= 0) return string.Empty;

            IReadOnlyList<EncodedToken> tokens = _Tokenizer.EncodeToTokens(text, out string? normalizedText);
            if (startTokenIndex >= tokens.Count) return string.Empty;

            int count = Math.Min(tokenCount, tokens.Count - startTokenIndex);
            if (count <= 0) return string.Empty;

            if (normalizedText == null || string.Equals(normalizedText, text, StringComparison.Ordinal))
            {
                int start = tokens[startTokenIndex].Offset.Start.GetOffset(text.Length);
                int end = tokens[startTokenIndex + count - 1].Offset.End.GetOffset(text.Length);
                start = Math.Max(0, Math.Min(start, text.Length));
                end = Math.Max(start, Math.Min(end, text.Length));
                if (start > 0 && start < text.Length && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1])) start--;
                if (end > 0 && end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1])) end++;
                return text.Substring(start, end - start);
            }

            string decoded = _Tokenizer.Decode(tokens.Skip(startTokenIndex).Take(count).Select(token => token.Id).ToArray()) ?? string.Empty;
            if (text.IndexOf('\uFFFD') < 0) decoded = decoded.Trim('\uFFFD');
            return decoded;
        }
    }
}
