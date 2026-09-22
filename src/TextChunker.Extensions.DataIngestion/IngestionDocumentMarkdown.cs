namespace TextChunker.Extensions.DataIngestion
{
    using System;
    using System.Text;
    using global::Microsoft.Extensions.DataIngestion;

    /// <summary>
    /// Reconstructs a markdown representation of an ingestion document by walking its elements in order.
    /// Headers become markdown headings so hierarchy aware and markdown aware chunking can find them, tables
    /// become pipe tables, and every other element contributes its rendered markdown. The result is fed to
    /// TextChunker.
    /// </summary>
    internal static class IngestionDocumentMarkdown
    {
        internal static string Build(IngestionDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            StringBuilder builder = new StringBuilder();

            foreach (IngestionDocumentElement element in document.EnumerateContent())
            {
                switch (element)
                {
                    case IngestionDocumentHeader header:
                        int level = header.Level.HasValue ? Math.Min(6, Math.Max(1, header.Level.Value)) : 1;
                        string headerText = TextOf(header);
                        if (headerText.Length > 0)
                            Append(builder, new string('#', level) + " " + headerText.TrimStart('#', ' '));
                        break;
                    case IngestionDocumentTable table:
                        string markdownTable = BuildTable(table);
                        if (markdownTable.Length > 0) Append(builder, markdownTable);
                        break;
                    case IngestionDocumentImage image:
                        string imageText = !string.IsNullOrWhiteSpace(image.AlternativeText)
                            ? image.AlternativeText!.Trim()
                            : TextOf(image);
                        if (imageText.Length > 0) Append(builder, imageText);
                        break;
                    default:
                        string text = TextOf(element);
                        if (text.Length > 0) Append(builder, text);
                        break;
                }
            }

            return builder.ToString().TrimEnd('\n');
        }

        private static string TextOf(IngestionDocumentElement element)
        {
            string markdown = element.GetMarkdown();
            if (!string.IsNullOrWhiteSpace(markdown)) return markdown.Trim();
            return element.Text != null ? element.Text.Trim() : string.Empty;
        }

        private static void Append(StringBuilder builder, string text)
        {
            builder.Append(text);
            builder.Append("\n\n");
        }

        private static string BuildTable(IngestionDocumentTable table)
        {
            IngestionDocumentElement?[,] cells = table.Cells;
            int rows = cells.GetLength(0);
            int columns = cells.GetLength(1);
            if (rows == 0 || columns == 0) return string.Empty;

            StringBuilder builder = new StringBuilder();
            AppendRow(builder, cells, 0, columns);

            builder.Append('|');
            for (int c = 0; c < columns; c++) builder.Append("---|");
            builder.Append('\n');

            for (int r = 1; r < rows; r++)
                AppendRow(builder, cells, r, columns);

            return builder.ToString().TrimEnd('\n');
        }

        private static void AppendRow(StringBuilder builder, IngestionDocumentElement?[,] cells, int row, int columns)
        {
            builder.Append("| ");
            for (int c = 0; c < columns; c++)
            {
                IngestionDocumentElement? cell = cells[row, c];
                string value = cell != null ? TextOf(cell) : string.Empty;
                builder.Append(value.Replace("|", "\\|"));
                builder.Append(" |");
                if (c < columns - 1) builder.Append(' ');
            }
            builder.Append('\n');
        }
    }
}
