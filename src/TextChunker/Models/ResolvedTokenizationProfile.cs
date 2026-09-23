namespace TextChunker.Models
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Enums;

    /// <summary>
    /// Resolved tokenization contract used for chunking, budgeting, and diagnostics.
    /// Produced by the tokenization profile resolver from caller options, provider defaults, and optional calibration.
    /// </summary>
    public class ResolvedTokenizationProfile
    {
        private int _MaxInputTokens = 1;
        private int _ReservedInputTokens = 0;
        private int _EffectiveInputBudget = 1;
        private string _TokenizerModel = "cl100k_base";
        private Dictionary<string, string> _ProviderMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Active tokenizer family. Never Auto on a resolved profile.
        /// </summary>
        public TokenizerKindEnum TokenizerKind { get; set; } = TokenizerKindEnum.Cl100kBase;

        /// <summary>
        /// Active tokenizer model or vocabulary identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or whitespace.</exception>
        public string TokenizerModel
        {
            get => _TokenizerModel;
            set => _TokenizerModel = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentNullException(nameof(TokenizerModel))
                : value;
        }

        /// <summary>
        /// Upstream maximum accepted input tokens. Minimum 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int MaxInputTokens
        {
            get => _MaxInputTokens;
            set => _MaxInputTokens = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxInputTokens), "MaxInputTokens must be at least 1.");
        }

        /// <summary>
        /// Tokens reserved before chunking begins. Minimum 0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0.</exception>
        public int ReservedInputTokens
        {
            get => _ReservedInputTokens;
            set => _ReservedInputTokens = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(ReservedInputTokens), "ReservedInputTokens must be at least 0.");
        }

        /// <summary>
        /// Effective per input chunking budget after reserved tokens are removed. Minimum 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int EffectiveInputBudget
        {
            get => _EffectiveInputBudget;
            set => _EffectiveInputBudget = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(EffectiveInputBudget), "EffectiveInputBudget must be at least 1.");
        }

        /// <summary>
        /// Source of the resolved profile.
        /// </summary>
        public TokenizationProfileSourceEnum ProfileSource { get; set; } = TokenizationProfileSourceEnum.GlobalFallback;

        /// <summary>
        /// True when resolution fell back to an API format provider default or the global fallback. False for an
        /// explicit override, a calibration result, or an exact known embedding model match, since those are
        /// authoritative rather than guessed. Inspect <see cref="ProfileSource"/> for the exact origin.
        /// </summary>
        public bool UsedFallback { get; set; } = false;

        /// <summary>
        /// Provider specific metadata captured during resolution. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public Dictionary<string, string> ProviderMetadata
        {
            get => _ProviderMetadata;
            set => _ProviderMetadata = value ?? throw new ArgumentNullException(nameof(ProviderMetadata));
        }
    }
}
