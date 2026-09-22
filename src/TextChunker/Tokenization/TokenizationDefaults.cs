namespace TextChunker.Tokenization
{
    using System;
    using TextChunker.Enums;

    /// <summary>
    /// Static registry of provider default token budgets and the model name heuristics used to
    /// pick a tokenizer family when the caller does not specify one.
    /// The budget values are exposed as settable properties so a consumer can adjust them without a code change.
    /// </summary>
    public static class TokenizationDefaults
    {
        /// <summary>
        /// Maximum input tokens assumed for OpenAI and vLLM style endpoints. Default 8192.
        /// </summary>
        public static int OpenAiMaxInputTokens { get; set; } = 8192;

        /// <summary>
        /// Maximum input tokens assumed for Gemini endpoints. Default 2048.
        /// </summary>
        public static int GeminiMaxInputTokens { get; set; } = 2048;

        /// <summary>
        /// Maximum input tokens assumed for BERT family WordPiece models. Default 512.
        /// </summary>
        public static int BertMaxInputTokens { get; set; } = 512;

        /// <summary>
        /// Maximum input tokens assumed by the global fallback when no provider default applies. Default 8192.
        /// </summary>
        public static int GlobalFallbackMaxInputTokens { get; set; } = 8192;

        /// <summary>
        /// Resolve a provider default entry for the supplied API format and model, or null when no provider
        /// default applies and the caller should use the global fallback.
        /// </summary>
        /// <param name="apiFormat">Upstream provider API format.</param>
        /// <param name="model">Model identifier, used for BERT family detection.</param>
        /// <returns>A provider default entry, or null.</returns>
        public static TokenizationDefaultEntry? Resolve(ApiFormatEnum apiFormat, string? model)
        {
            bool bertLike = IsBertLikeModel(model);
            bool o200kLike = IsO200kModel(model);

            switch (apiFormat)
            {
                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.VLLM:
                    return o200kLike
                        ? new TokenizationDefaultEntry(TokenizerKindEnum.O200kBase, "o200k_base", OpenAiMaxInputTokens)
                        : new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", OpenAiMaxInputTokens);
                case ApiFormatEnum.Gemini:
                    return new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", GeminiMaxInputTokens);
                case ApiFormatEnum.Ollama:
                    return bertLike
                        ? new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", BertMaxInputTokens)
                        : new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", OpenAiMaxInputTokens);
                default:
                    if (bertLike)
                        return new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", BertMaxInputTokens);
                    if (o200kLike)
                        return new TokenizationDefaultEntry(TokenizerKindEnum.O200kBase, "o200k_base", OpenAiMaxInputTokens);
                    return null;
            }
        }

        /// <summary>
        /// Determine whether a model identifier names an OpenAI model that uses the o200k_base tokenizer.
        /// </summary>
        /// <param name="model">Model identifier.</param>
        /// <returns>True when the model appears to use o200k_base.</returns>
        public static bool IsO200kModel(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return false;
            string value = model!.Trim().ToLowerInvariant();

            return value.Contains("o200k")
                || value.StartsWith("gpt-4o", StringComparison.Ordinal)
                || value.StartsWith("gpt-4.1", StringComparison.Ordinal)
                || value.StartsWith("gpt-5", StringComparison.Ordinal)
                || value.StartsWith("chatgpt-4o", StringComparison.Ordinal)
                || value.StartsWith("o1", StringComparison.Ordinal)
                || value.StartsWith("o3", StringComparison.Ordinal)
                || value.StartsWith("o4", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determine whether a model identifier names a BERT family WordPiece model.
        /// </summary>
        /// <param name="model">Model identifier.</param>
        /// <returns>True when the model appears to be BERT family.</returns>
        public static bool IsBertLikeModel(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return false;

            return model!.IndexOf("bert", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("minilm", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("e5", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("gte", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("bge", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
