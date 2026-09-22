namespace TextChunker.Benchmarks.Benchmarks
{
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Builds deterministic corpora of varying size for the benchmarks.
    /// </summary>
    internal static class CorpusFactory
    {
        internal static string Words(int count)
        {
            return string.Join(" ", Enumerable.Range(1, count).Select(i => "word" + i));
        }

        internal static string Prose(int paragraphs)
        {
            StringBuilder builder = new StringBuilder();
            for (int p = 0; p < paragraphs; p++)
            {
                builder.Append("The quick brown fox jumps over the lazy dog. ");
                builder.Append("It had been sleeping soundly for hours before the interruption occurred. ");
                builder.Append("Determinism matters more than anything else in this pipeline.");
                builder.Append("\n\n");
            }
            return builder.ToString();
        }
    }
}
