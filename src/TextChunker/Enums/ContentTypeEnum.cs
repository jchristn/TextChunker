namespace TextChunker.Enums
{
    /// <summary>
    /// Type of content being chunked. Drives strategy routing.
    /// </summary>
    public enum ContentTypeEnum
    {
        /// <summary>Plain text content.</summary>
        Text,
        /// <summary>List content.</summary>
        List,
        /// <summary>Binary content.</summary>
        Binary,
        /// <summary>Table content.</summary>
        Table,
        /// <summary>Unknown content type.</summary>
        Unknown,
        /// <summary>Image content.</summary>
        Image,
        /// <summary>Hyperlink content.</summary>
        Hyperlink,
        /// <summary>Code content.</summary>
        Code,
        /// <summary>Metadata content.</summary>
        Meta,
        /// <summary>Summary content.</summary>
        Summary
    }
}
