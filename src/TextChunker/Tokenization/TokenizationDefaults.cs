namespace TextChunker.Tokenization
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Enums;

    /// <summary>
    /// Static registry of provider default token budgets, a configurable table of well known embedding models,
    /// and the model name heuristics used to pick a tokenizer family when the caller does not specify one.
    /// The budget values and the known model table are exposed for mutation so a consumer can adjust or extend
    /// them without a code change.
    /// </summary>
    public static class TokenizationDefaults
    {
        private static readonly Dictionary<string, TokenizationDefaultEntry> _KnownModels =
            new Dictionary<string, TokenizationDefaultEntry>(StringComparer.OrdinalIgnoreCase);

        static TokenizationDefaults()
        {
            SeedKnownModels();
        }

        /// <summary>
        /// Maximum input tokens assumed for OpenAI and vLLM style endpoints. Default 8192.
        /// </summary>
        public static int OpenAiMaxInputTokens { get; set; } = 8192;

        /// <summary>
        /// Maximum input tokens assumed for Gemini endpoints. Default 2048.
        /// </summary>
        public static int GeminiMaxInputTokens { get; set; } = 2048;

        /// <summary>
        /// Maximum input tokens assumed for BERT family WordPiece models that are not matched by the known model
        /// table. Default 512.
        /// </summary>
        public static int BertMaxInputTokens { get; set; } = 512;

        /// <summary>
        /// Maximum input tokens assumed by the global fallback when no provider default applies. Default 8192.
        /// </summary>
        public static int GlobalFallbackMaxInputTokens { get; set; } = 8192;

        /// <summary>
        /// Tokens reserved for BERT family WordPiece models so that a full chunk still fits once the embedding
        /// endpoint prepends [CLS] and appends [SEP]. The offline tokenizer does not count these special tokens, so
        /// the effective chunking budget is the model maximum minus this reservation. Default 2.
        /// </summary>
        public static int BertReservedInputTokens { get; set; } = 2;

        /// <summary>
        /// Configurable table of well known embedding models keyed by a lowercase match token. During resolution a
        /// model identifier is normalized (provider path prefixes and version or quantization tags are removed) and
        /// matched against these keys, longest key first, so a specific entry wins over a generic one. Add or replace
        /// entries with <see cref="RegisterKnownModel(string, TokenizerKindEnum, string, int, int)"/> or by mutating
        /// this dictionary directly. Removing an entry falls that model back to the family heuristics.
        /// </summary>
        public static IDictionary<string, TokenizationDefaultEntry> KnownModels => _KnownModels;

        /// <summary>
        /// Register or replace a well known embedding model entry so its tokenizer family and budgets are applied out
        /// of the box whenever a matching model identifier is resolved.
        /// </summary>
        /// <param name="modelMatchToken">Lowercase substring matched against the normalized model identifier, for
        /// example "nomic-embed-text" or "all-minilm-l6-v2".</param>
        /// <param name="tokenizerKind">Tokenizer family to apply.</param>
        /// <param name="tokenizerModel">Tokenizer model or vocabulary identifier to apply.</param>
        /// <param name="maxInputTokens">Maximum accepted input tokens for the model.</param>
        /// <param name="reservedInputTokens">Tokens reserved off the top of the maximum input budget. Use 2 for
        /// BERT family models to leave room for the [CLS] and [SEP] special tokens the embedding endpoint adds.</param>
        /// <exception cref="ArgumentException">Thrown when the match token is null or whitespace.</exception>
        public static void RegisterKnownModel(
            string modelMatchToken,
            TokenizerKindEnum tokenizerKind,
            string tokenizerModel,
            int maxInputTokens,
            int reservedInputTokens = 0)
        {
            if (string.IsNullOrWhiteSpace(modelMatchToken))
                throw new ArgumentException("Model match token must be provided.", nameof(modelMatchToken));

            _KnownModels[modelMatchToken.Trim().ToLowerInvariant()] =
                new TokenizationDefaultEntry(tokenizerKind, tokenizerModel, maxInputTokens, reservedInputTokens);
        }

        /// <summary>
        /// Resolve a provider default entry for the supplied API format and model, or null when no provider
        /// default applies and the caller should use the global fallback. A match in the known model table takes
        /// precedence over the API format, since the model identifier is the more authoritative signal.
        /// </summary>
        /// <param name="apiFormat">Upstream provider API format.</param>
        /// <param name="model">Model identifier, used for known model and BERT family detection.</param>
        /// <returns>A provider default entry, or null.</returns>
        public static TokenizationDefaultEntry? Resolve(ApiFormatEnum apiFormat, string? model)
        {
            TokenizationDefaultEntry? known = ResolveKnownModel(model);
            if (known != null) return known;

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
                        ? new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", BertMaxInputTokens, BertReservedInputTokens)
                        : new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", OpenAiMaxInputTokens);
                default:
                    if (bertLike)
                        return new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", BertMaxInputTokens, BertReservedInputTokens);
                    if (o200kLike)
                        return new TokenizationDefaultEntry(TokenizerKindEnum.O200kBase, "o200k_base", OpenAiMaxInputTokens);
                    return null;
            }
        }

        /// <summary>
        /// Resolve a well known embedding model entry from the configurable table, or null when the model identifier
        /// does not match any registered entry.
        /// </summary>
        /// <param name="model">Model identifier.</param>
        /// <returns>A copy of the matched entry, or null.</returns>
        public static TokenizationDefaultEntry? ResolveKnownModel(string? model)
        {
            string normalized = NormalizeModelId(model);
            if (normalized.Length == 0 || _KnownModels.Count == 0) return null;

            TokenizationDefaultEntry? best = null;
            int bestKeyLength = -1;

            foreach (KeyValuePair<string, TokenizationDefaultEntry> pair in _KnownModels)
            {
                if (pair.Key.Length > bestKeyLength
                    && normalized.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                {
                    best = pair.Value;
                    bestKeyLength = pair.Key.Length;
                }
            }

            if (best == null) return null;

            return new TokenizationDefaultEntry(
                best.TokenizerKind,
                best.TokenizerModel,
                best.MaxInputTokens,
                best.ReservedInputTokens);
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
                || model.IndexOf("mpnet", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("nomic", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("mxbai", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("e5", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("gte", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("bge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Normalize a model identifier for known model matching by lowercasing, stripping any provider path prefix
        /// (for example "sentence-transformers/") and stripping any version or quantization tag (for example
        /// ":latest" or ":q4_0").
        /// </summary>
        /// <param name="model">Raw model identifier.</param>
        /// <returns>The normalized identifier, or an empty string when the input is null or whitespace.</returns>
        private static string NormalizeModelId(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return string.Empty;

            string value = model!.Trim().ToLowerInvariant();

            int slash = value.LastIndexOf('/');
            if (slash >= 0 && slash < value.Length - 1)
                value = value.Substring(slash + 1);

            int colon = value.IndexOf(':');
            if (colon >= 0)
                value = value.Substring(0, colon);

            return value;
        }

        private static void SeedKnownModels()
        {
            // OpenAI embedding models: cl100k_base, 8191 token input limit.
            _KnownModels["text-embedding-3-large"] = new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", 8191);
            _KnownModels["text-embedding-3-small"] = new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", 8191);
            _KnownModels["text-embedding-ada-002"] = new TokenizationDefaultEntry(TokenizerKindEnum.Cl100kBase, "cl100k_base", 8191);

            // Nomic embedding models: BERT WordPiece, 2048 token sequence length.
            _KnownModels["nomic-embed-text"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 2048, 2);
            _KnownModels["nomic-embed"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 2048, 2);

            // Sentence-Transformers MiniLM models: BERT WordPiece, truncated to the model sequence length.
            _KnownModels["all-minilm-l6-v2"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 256, 2);
            _KnownModels["all-minilm-l12-v2"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 256, 2);
            _KnownModels["paraphrase-minilm"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 128, 2);
            _KnownModels["all-minilm"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 256, 2);
            _KnownModels["minilm"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 256, 2);

            // MPNet models: BERT WordPiece, 384 or 512 token sequence length.
            _KnownModels["all-mpnet-base-v2"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 384, 2);
            _KnownModels["multi-qa-mpnet-base"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["mpnet"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 384, 2);

            // Mixedbread mxbai embedding models: BERT WordPiece, 512 token sequence length.
            _KnownModels["mxbai-embed-large"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["mxbai-embed"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);

            // BAAI bge models: BERT WordPiece, 512 token sequence length (bge-m3 extends to 8192).
            _KnownModels["bge-m3"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 8192, 2);
            _KnownModels["bge-large"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["bge-base"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["bge-small"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["bge"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);

            // Alibaba gte models: BERT WordPiece, 512 token sequence length.
            _KnownModels["gte-large"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["gte-base"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["gte-small"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["gte"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);

            // Microsoft E5 models: BERT WordPiece, 512 token sequence length.
            _KnownModels["multilingual-e5"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["e5-large"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["e5-base"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
            _KnownModels["e5-small"] = new TokenizationDefaultEntry(TokenizerKindEnum.BertWordPiece, "bert-base-uncased", 512, 2);
        }
    }
}
