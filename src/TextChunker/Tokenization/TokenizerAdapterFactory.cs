namespace TextChunker.Tokenization
{
    using System;
    using TextChunker.Enums;
    using TextChunker.Exceptions;
    using TextChunker.Models;

    /// <summary>
    /// Creates tokenizer adapters for resolved tokenization profiles.
    /// </summary>
    public static class TokenizerAdapterFactory
    {
        /// <summary>
        /// Create a tokenizer adapter for the supplied resolved profile.
        /// </summary>
        /// <param name="profile">Resolved tokenization profile.</param>
        /// <returns>A tokenizer adapter for the profile's tokenizer family.</returns>
        /// <exception cref="ArgumentNullException">Thrown when profile is null.</exception>
        /// <exception cref="TokenizerResolutionException">Thrown when the tokenizer family cannot be created.</exception>
        public static ITokenizerAdapter Create(ResolvedTokenizationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            switch (profile.TokenizerKind)
            {
                case TokenizerKindEnum.Cl100kBase:
                    return new SharpTokenTokenizerAdapter(
                        string.IsNullOrWhiteSpace(profile.TokenizerModel) ? "cl100k_base" : profile.TokenizerModel);
                case TokenizerKindEnum.O200kBase:
                    return new SharpTokenTokenizerAdapter(
                        string.IsNullOrWhiteSpace(profile.TokenizerModel) ? "o200k_base" : profile.TokenizerModel);
                case TokenizerKindEnum.BertWordPiece:
                    return new BertWordPieceTokenizerAdapter();
                default:
                    throw new TokenizerResolutionException(
                        "Unsupported tokenizer kind for adapter creation: " + profile.TokenizerKind
                        + ". Expected Cl100kBase, O200kBase, or BertWordPiece. For Hugging Face or SentencePiece models, "
                        + "construct a Chunker with an MlTokenizerAdapter instead.");
            }
        }
    }
}
