namespace TextChunker.Enums
{
    /// <summary>
    /// Upstream embedding provider API format, used to resolve provider default token budgets.
    /// </summary>
    public enum ApiFormatEnum
    {
        /// <summary>Unknown or unspecified provider.</summary>
        Unknown,
        /// <summary>OpenAI compatible API.</summary>
        OpenAI,
        /// <summary>vLLM served model.</summary>
        VLLM,
        /// <summary>Google Gemini API.</summary>
        Gemini,
        /// <summary>Ollama served model.</summary>
        Ollama
    }
}
