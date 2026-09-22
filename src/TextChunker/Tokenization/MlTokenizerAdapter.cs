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

        /// <inheritdoc />
        public string SliceByTokenRange(string text, int startTokenIndex, int tokenCount)
        {
            if (string.IsNullOrEmpty(text) || tokenCount <= 0) return string.Empty;
            if (startTokenIndex < 0) throw new ArgumentOutOfRangeException(nameof(startTokenIndex));

            IReadOnlyList<int> ids = _Tokenizer.EncodeToIds(text);
            if (startTokenIndex >= ids.Count) return string.Empty;

            int count = Math.Min(tokenCount, ids.Count - startTokenIndex);
            if (count <= 0) return string.Empty;

            return _Tokenizer.Decode(ids.Skip(startTokenIndex).Take(count).ToArray());
        }
    }
}
