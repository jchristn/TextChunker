namespace TextChunker.Models
{
    using System;
    using System.Collections.Generic;
    using TextChunker.Enums;

    /// <summary>
    /// A structured chunking request describing one piece of content and, optionally, a hierarchy of child
    /// content. Use this to chunk pre parsed content that carries shape, such as lists, tables, and document
    /// hierarchies, with parent identity and labels flowing through to the produced chunks.
    /// </summary>
    public class ContentRequest
    {
        /// <summary>
        /// Unique identifier for this content. Auto generated when not supplied.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Parent identifier, or null when this content has no parent.
        /// </summary>
        public Guid? ParentGUID { get; set; } = null;

        /// <summary>
        /// Child content forming a hierarchy. Each is chunked in document order after this content's own text.
        /// </summary>
        public List<ContentRequest>? Children { get; set; } = null;

        /// <summary>
        /// Type of this content. Drives strategy routing.
        /// </summary>
        public ContentTypeEnum Type { get; set; } = ContentTypeEnum.Text;

        /// <summary>
        /// Text content, used for text content types.
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
