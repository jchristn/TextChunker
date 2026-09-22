namespace TextChunker.Evaluation
{
    using System.Collections.Generic;

    /// <summary>
    /// A document under evaluation together with its golden relevant spans.
    /// </summary>
    internal sealed class EvalDocument
    {
        internal string Id { get; }

        internal string Text { get; }

        internal List<EvalSpan> Spans { get; }

        internal EvalDocument(string id, string text, List<EvalSpan> spans)
        {
            Id = id;
            Text = text;
            Spans = spans;
        }
    }
}
