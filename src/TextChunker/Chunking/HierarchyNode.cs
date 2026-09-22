namespace TextChunker.Chunking
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A node in a header hierarchy built from markdown style headings. Each node owns the content lines that
    /// appear under its heading and before any deeper heading.
    /// </summary>
    internal class HierarchyNode
    {
        internal int Level { get; set; } = 0;

        internal string Title { get; set; } = string.Empty;

        internal HierarchyNode? Parent { get; set; } = null;

        internal List<HierarchyNode> Children { get; } = new List<HierarchyNode>();

        internal List<string> ContentLines { get; } = new List<string>();

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

        internal string GetContent()
        {
            return string.Join("\n", ContentLines).Trim();
        }
    }
}
