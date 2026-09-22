namespace TextChunker.Enums
{
    /// <summary>
    /// Supported tokenizer adapter families.
    /// </summary>
    public enum TokenizerKindEnum
    {
        /// <summary>Resolve the tokenizer automatically from the model or provider hint.</summary>
        Auto,
        /// <summary>OpenAI style cl100k_base BPE tokenization (GPT-3.5, GPT-4, text-embedding-3).</summary>
        Cl100kBase,
        /// <summary>OpenAI style o200k_base BPE tokenization (GPT-4o, GPT-4.1, o-series, GPT-5).</summary>
        O200kBase,
        /// <summary>BERT style WordPiece tokenization.</summary>
        BertWordPiece
    }
}
