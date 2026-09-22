namespace TextChunker.Tokenization
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Exceptions;
    using TextChunker.Models;

    /// <summary>
    /// Resolves a tokenization profile using a fixed precedence: explicit caller override, then an optional
    /// calibration probe against a live endpoint, then a provider default, then the global fallback.
    /// This type performs no network access on its own. Calibration is delegated to an optional probe.
    /// </summary>
    public class TokenizationProfileResolver
    {
        private readonly ITokenizerCalibrationProbe? _Probe;

        /// <summary>
        /// Initialize a new resolver.
        /// </summary>
        /// <param name="probe">Optional calibration probe. When null, calibration is skipped.</param>
        public TokenizationProfileResolver(ITokenizerCalibrationProbe? probe = null)
        {
            _Probe = probe;
        }

        /// <summary>
        /// Resolve the active tokenization profile.
        /// </summary>
        /// <param name="tokenizerKind">Requested tokenizer family. Auto lets the resolver choose from provider defaults.</param>
        /// <param name="apiFormat">Upstream provider API format hint.</param>
        /// <param name="modelId">Model identifier hint, used for BERT family detection.</param>
        /// <param name="effectiveInputBudgetOverride">Hard override of the effective input budget, or null.</param>
        /// <param name="allowCalibration">When true and a probe is configured, calibration is attempted.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The resolved tokenization profile.</returns>
        /// <exception cref="TokenizerResolutionException">Thrown when a supplied calibration probe fails.</exception>
        public async Task<ResolvedTokenizationProfile> ResolveAsync(
            TokenizerKindEnum tokenizerKind,
            ApiFormatEnum apiFormat,
            string? modelId,
            int? effectiveInputBudgetOverride,
            bool allowCalibration,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            ResolvedTokenizationProfile profile = new ResolvedTokenizationProfile
            {
                TokenizerKind = TokenizerKindEnum.Cl100kBase,
                TokenizerModel = "cl100k_base",
                MaxInputTokens = Math.Max(1, TokenizationDefaults.GlobalFallbackMaxInputTokens),
                ProfileSource = TokenizationProfileSourceEnum.GlobalFallback,
                UsedFallback = true
            };

            TokenizationDefaultEntry? providerDefault = TokenizationDefaults.Resolve(apiFormat, modelId);
            if (providerDefault != null)
            {
                profile.TokenizerKind = providerDefault.TokenizerKind;
                profile.TokenizerModel = providerDefault.TokenizerModel;
                profile.MaxInputTokens = Math.Max(1, providerDefault.MaxInputTokens);
                profile.ProfileSource = TokenizationProfileSourceEnum.ProviderDefault;
                profile.UsedFallback = true;
            }

            if (tokenizerKind != TokenizerKindEnum.Auto)
            {
                profile.TokenizerKind = tokenizerKind;
                profile.TokenizerModel = DefaultModelFor(tokenizerKind);
                if (tokenizerKind == TokenizerKindEnum.BertWordPiece && providerDefault == null)
                    profile.MaxInputTokens = Math.Max(1, TokenizationDefaults.BertMaxInputTokens);
                profile.ProfileSource = TokenizationProfileSourceEnum.Override;
                profile.UsedFallback = false;
            }

            if (effectiveInputBudgetOverride.HasValue)
            {
                profile.EffectiveInputBudget = Math.Max(1, Math.Min(profile.MaxInputTokens, effectiveInputBudgetOverride.Value));
                profile.ReservedInputTokens = Math.Max(0, profile.MaxInputTokens - profile.EffectiveInputBudget);
                profile.ProfileSource = TokenizationProfileSourceEnum.Override;
                profile.UsedFallback = false;
            }
            else
            {
                profile.EffectiveInputBudget = Math.Max(1, profile.MaxInputTokens - profile.ReservedInputTokens);
            }

            if (_Probe != null && allowCalibration && !effectiveInputBudgetOverride.HasValue)
            {
                try
                {
                    ITokenizerAdapter provisional = TokenizerAdapterFactory.Create(profile);
                    TokenizationCalibrationResult? result = await _Probe
                        .CalibrateAsync(profile, provisional, token)
                        .ConfigureAwait(false);

                    if (result != null && result.EffectiveInputBudget >= 1)
                    {
                        profile.EffectiveInputBudget = Math.Max(1, Math.Min(profile.MaxInputTokens, result.EffectiveInputBudget));
                        profile.ReservedInputTokens = Math.Max(0, profile.MaxInputTokens - profile.EffectiveInputBudget);
                        if (result.BatchLimitMode != BatchLimitModeEnum.Unknown)
                            profile.BatchLimitMode = result.BatchLimitMode;
                        profile.ProfileSource = TokenizationProfileSourceEnum.Calibration;
                        profile.UsedFallback = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new TokenizerResolutionException("Calibration probe failed during profile resolution: " + ex.Message, ex);
                }
            }

            profile.ProviderMetadata["ApiFormat"] = apiFormat.ToString();
            profile.ProviderMetadata["Model"] = modelId ?? string.Empty;
            profile.ProviderMetadata["ResolvedMaxInputTokens"] = profile.MaxInputTokens.ToString();
            profile.ProviderMetadata["ResolvedEffectiveInputBudget"] = profile.EffectiveInputBudget.ToString();
            profile.ProviderMetadata["ProfileSource"] = profile.ProfileSource.ToString();

            return profile;
        }

        private static string DefaultModelFor(TokenizerKindEnum kind)
        {
            switch (kind)
            {
                case TokenizerKindEnum.O200kBase:
                    return "o200k_base";
                case TokenizerKindEnum.BertWordPiece:
                    return "bert-base-uncased";
                default:
                    return "cl100k_base";
            }
        }
    }
}
