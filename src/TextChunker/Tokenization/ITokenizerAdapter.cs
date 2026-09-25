namespace TextChunker.Tokenization
{
    using System.Collections.Generic;

    /// <summary>
    /// Provider agnostic tokenizer operations. The chunking strategies call only <see cref="CountTokens"/>, so an
    /// accurate count is what matters most for a custom implementation. Implementations must be safe for
    /// concurrent read use.
    /// </summary>
    public interface ITokenizerAdapter
    {
        /// <summary>
        /// Count the number of tokens the supplied text encodes to.
        /// </summary>
        /// <param name="text">Text to measure. A null or empty string returns 0.</param>
        /// <returns>Token count, never negative.</returns>
        int CountTokens(string text);

        /// <summary>
        /// Encode text into tokenizer native token identifiers.
        /// </summary>
        /// <param name="text">Text to encode. A null or empty string returns an empty list.</param>
        /// <returns>Read only list of token identifiers.</returns>
        IReadOnlyList<int> Encode(string text);

        /// <summary>
        /// Decode tokenizer native token identifiers back into text.
        /// </summary>
        /// <param name="tokenIds">Token identifiers to decode.</param>
        /// <returns>Decoded text.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when tokenIds is null.</exception>
        string Decode(IEnumerable<int> tokenIds);

        /// <summary>
        /// Return the text covered by a range of tokenizer native tokens. The shipped adapters return the exact
        /// substring of the input that the tokens cover, never splitting a surrogate pair; an implementation that
        /// cannot map tokens back to the input may return decoded text instead.
        /// </summary>
        /// <param name="text">Source text.</param>
        /// <param name="startTokenIndex">Zero based token index to start from. Must be at least 0.</param>
        /// <param name="tokenCount">Number of tokens to include. Values of 0 or less return an empty string.</param>
        /// <returns>Text for the requested token range.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when startTokenIndex is negative.</exception>
        string SliceByTokenRange(string text, int startTokenIndex, int tokenCount);
    }
}
