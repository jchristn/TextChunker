namespace TextChunker.Chunking
{
    using System.Collections.Generic;

    /// <summary>
    /// A node in a header hierarchy built from markdown style headings. Each node owns the range of source text that
    /// appears under its heading and before the next heading.
    /// </summary>
    internal class HierarchyNode
    {
        internal int Level { get; set; } = 0;

        internal string Title { get; set; } = string.Empty;

        internal HierarchyNode? Parent { get; set; } = null;

        internal List<HierarchyNode> Children { get; } = new List<HierarchyNode>();

        internal int ContentStart { get; set; } = -1;

        internal int ContentEnd { get; set; } = -1;

        internal string BuildBreadcrumb(string separator)
        {
            List<string> titles = new List<string>();
            HierarchyNode? current = this;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Title))
                    titles.Add(current.Title.Trim());
                current = current.Parent;
            }

            titles.Reverse();
            return string.Join(separator, titles);
        }

        internal SourceSpan GetContentSpan(string source)
        {
            if (ContentStart < 0 || ContentEnd <= ContentStart) return new SourceSpan(0, 0);
            return ChunkingHelpers.Trim(source, new SourceSpan(ContentStart, ContentEnd));
        }
    }
}
