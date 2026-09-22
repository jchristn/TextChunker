namespace TextChunker.Evaluation
{
    /// <summary>
    /// A golden relevant span within a document: the character range a query's answer occupies.
    /// </summary>
    internal sealed class EvalSpan
    {
        internal string Label { get; }

        internal int Start { get; }

        internal int Length { get; }

        internal int End => Start + Length;

        internal EvalSpan(string label, int start, int length)
        {
            Label = label;
            Start = start;
            Length = length;
        }
    }
}
