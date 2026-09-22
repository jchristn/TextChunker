namespace TextChunker.Evaluation
{
    /// <summary>
    /// Aggregated evaluation metrics for one chunking configuration across the corpus.
    /// </summary>
    internal sealed class ConfigScore
    {
        internal string Name { get; set; } = string.Empty;

        internal double Recall { get; set; } = 0.0;

        internal double Precision { get; set; } = 0.0;

        internal double Iou { get; set; } = 0.0;

        internal double MeanChunks { get; set; } = 0.0;

        internal double MeanTokens { get; set; } = 0.0;
    }
}
