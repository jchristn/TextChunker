namespace TextChunker.Models
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Enums;

    /// <summary>
    /// A single produced chunk. Carries identity, ordering, sizing, provenance, and optional integrity data so a
    /// chunk can be grouped by parent, ordered, re embedded, cited back to its source span, and de duplicated
    /// without a second data structure.
    /// </summary>
    public class Chunk
    {
        private string _Text = string.Empty;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private List<float> _Embeddings = new List<float>();

        /// <summary>
        /// Unique identifier for this chunk.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Identifier of the parent this chunk was produced from (the caller supplied parent, or the parent of
        /// the source content). Null when no parent was supplied.
        /// </summary>
        public Guid? ParentGUID { get; set; } = null;

        /// <summary>
        /// Zero based ordinal of this chunk within its parent.
        /// </summary>
        public int Position { get; set; } = 0;

        /// <summary>
        /// Chunk text. Never null. Setting null coerces to an empty string.
        /// </summary>
        public string Text
        {
            get => _Text;
            set => _Text = value ?? string.Empty;
        }

        /// <summary>
        /// Token count of the chunk under the resolved tokenizer. Set to 0 when token counting is disabled.
        /// </summary>
        public int TokenCount { get; set; } = 0;

        /// <summary>
        /// Character count of the chunk text.
        /// </summary>
        public int CharacterCount { get; set; } = 0;

        /// <summary>
        /// Character offset of the chunk within the source text, or -1 when the chunk text is not a literal
        /// substring of the source (for example serialized list or table output).
        /// </summary>
        public int StartOffset { get; set; } = -1;

        /// <summary>
        /// Exclusive end character offset of the chunk within the source text, or -1 when not resolvable.
        /// </summary>
        public int EndOffset { get; set; } = -1;

        /// <summary>
        /// The strategy that produced this chunk.
        /// </summary>
        public ChunkStrategyEnum Strategy { get; set; } = ChunkStrategyEnum.FixedTokenCount;

        /// <summary>
        /// The tokenizer model or vocabulary identifier used to size this chunk, for reproducibility.
        /// </summary>
        public string TokenizerModel { get; set; } = string.Empty;

        /// <summary>
        /// Breadcrumb header context such as "Guide &gt; Setup &gt; Windows", or null unless hierarchy aware
        /// chunking was requested.
        /// </summary>
        public string? HeaderContext { get; set; } = null;

        /// <summary>
        /// Caller supplied labels, echoed through from the request. Never null.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Caller supplied metadata, echoed through from the request. Never null.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Reserved for an embedding vector computed by a downstream embedder. Never null.
        /// </summary>
        public List<float> Embeddings
        {
            get => _Embeddings;
            set => _Embeddings = value ?? new List<float>();
        }

        /// <summary>
        /// MD5 hash over the UTF-8 bytes of the chunk text, or null unless hash computation was requested.
        /// </summary>
        public byte[]? MD5Hash { get; set; } = null;

        /// <summary>
        /// SHA1 hash over the UTF-8 bytes of the chunk text, or null unless hash computation was requested.
        /// </summary>
        public byte[]? SHA1Hash { get; set; } = null;

        /// <summary>
        /// SHA256 hash over the UTF-8 bytes of the chunk text, or null unless hash computation was requested.
        /// </summary>
        public byte[]? SHA256Hash { get; set; } = null;
    }
}
