namespace TextChunker.Enums
{
    /// <summary>
    /// Content format hint for the Recursive strategy. Selects a structure aware separator ladder so headings,
    /// code declarations, and paragraphs are preferred as split points.
    /// </summary>
    public enum ContentFormatEnum
    {
        /// <summary>Plain prose. Splits on paragraphs, lines, sentences, then words.</summary>
        Plain,
        /// <summary>Markdown. Prefers heading and fenced code boundaries.</summary>
        Markdown,
        /// <summary>C# source. Prefers namespace, type, and member declarations.</summary>
        CSharp,
        /// <summary>Python source. Prefers class and function declarations.</summary>
        Python,
        /// <summary>JavaScript or TypeScript source. Prefers function, class, and block declarations.</summary>
        JavaScript,
        /// <summary>Java source. Prefers type and member declarations.</summary>
        Java
    }
}
