namespace TextChunker.Models
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Enums;

    /// <summary>
    /// A structured chunking request describing one content cell and, optionally, a hierarchy of child cells.
    /// Use this to chunk pre parsed content that carries shape, such as lists, tables, and document hierarchies,
    /// with parent identity and labels flowing through to the produced chunks.
    /// </summary>
    public class SemanticCellRequest
    {
        /// <summary>
        /// Unique identifier for this cell. Auto generated when not supplied.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Parent cell identifier, or null for a root cell.
        /// </summary>
        public Guid? ParentGUID { get; set; } = null;

        /// <summary>
        /// Child cells forming a hierarchy. Each is chunked in document order after this cell's own content.
        /// </summary>
        public List<SemanticCellRequest>? Children { get; set; } = null;

        /// <summary>
        /// Type of the content in this cell. Drives strategy routing.
        /// </summary>
        public AtomTypeEnum Type { get; set; } = AtomTypeEnum.Text;

        /// <summary>
        /// Text content, used for text atom types.
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Unordered list content.
        /// </summary>
        public List<string>? UnorderedList { get; set; } = null;

        /// <summary>
        /// Ordered list content. Takes precedence over UnorderedList when both are set.
        /// </summary>
        public List<string>? OrderedList { get; set; } = null;

        /// <summary>
        /// Table content as a list of rows, each a list of cell values. Row zero is treated as the header.
        /// </summary>
        public List<List<string>>? Table { get; set; } = null;

        /// <summary>
        /// Labels echoed onto each produced chunk.
        /// </summary>
        public List<string>? Labels { get; set; } = null;

        /// <summary>
        /// Tags echoed onto each produced chunk.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; } = null;
    }
}
