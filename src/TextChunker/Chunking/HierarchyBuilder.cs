namespace TextChunker.Chunking
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Builds a header hierarchy tree from markdown style text so that hierarchy aware chunking can stamp each
    /// chunk with a breadcrumb such as "Guide &gt; Setup &gt; Windows".
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
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            foreach (string line in lines)
            {
                Match match = _HeaderRegex.Match(line);
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
                    current.ContentLines.Add(line);
                }
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
