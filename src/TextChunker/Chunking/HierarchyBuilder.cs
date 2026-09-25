namespace TextChunker.Chunking
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Builds a header hierarchy tree from markdown style text so that hierarchy aware chunking can stamp each
    /// chunk with a breadcrumb such as "Guide &gt; Setup &gt; Windows". Content ranges are recorded in the original
    /// text, so section chunks keep exact source offsets.
    /// </summary>
    internal static class HierarchyBuilder
    {
        private static readonly Regex _HeaderRegex = new Regex(@"^(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);

        internal static HierarchyNode Build(string text)
        {
            HierarchyNode root = new HierarchyNode
            {
                Level = 0,
                Title = string.Empty
            };

            if (string.IsNullOrEmpty(text)) return root;

            HierarchyNode current = root;
            int lineStart = 0;
            while (lineStart <= text.Length)
            {
                int newline = lineStart < text.Length ? text.IndexOf('\n', lineStart) : -1;
                int lineEnd = newline < 0 ? text.Length : newline;
                int contentEnd = lineEnd > lineStart && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;

                Match match = _HeaderRegex.Match(text.Substring(lineStart, contentEnd - lineStart));
                if (match.Success)
                {
                    int level = match.Groups[1].Value.Length;
                    string title = match.Groups[2].Value.Trim();

                    HierarchyNode parent = current;
                    while (parent.Level >= level && parent.Parent != null)
                        parent = parent.Parent;

                    HierarchyNode node = new HierarchyNode
                    {
                        Level = level,
                        Title = title,
                        Parent = parent
                    };
                    parent.Children.Add(node);
                    current = node;
                }
                else
                {
                    if (current.ContentStart < 0) current.ContentStart = lineStart;
                    current.ContentEnd = contentEnd;
                }

                if (newline < 0) break;
                lineStart = newline + 1;
            }

            return root;
        }

        internal static List<HierarchyNode> Flatten(HierarchyNode root)
        {
            List<HierarchyNode> ordered = new List<HierarchyNode>();
            Visit(root, ordered);
            return ordered;
        }

        private static void Visit(HierarchyNode node, List<HierarchyNode> ordered)
        {
            ordered.Add(node);
            foreach (HierarchyNode child in node.Children)
                Visit(child, ordered);
        }
    }
}
