namespace TextChunker.Evaluation
{
    using System.Collections.Generic;
    using System.Linq;
    using TextChunker.Chunking;
    using TextChunker.Models;

    /// <summary>
    /// Computes token span overlap metrics for a chunking configuration. Retrieval is held fixed by an oracle
    /// that selects the chunks actually overlapping each golden span, so the metric isolates chunk boundary
    /// quality: recall measures coverage of the answer, precision measures how little surrounding noise the
    /// covering chunks add, and IoU combines both.
    /// </summary>
    internal static class Evaluator
    {
        internal static ConfigScore Evaluate(string name, List<EvalDocument> documents, ChunkingOptions options)
        {
            Chunker chunker = new Chunker();

            double recallSum = 0.0;
            double precisionSum = 0.0;
            double iouSum = 0.0;
            int spanCount = 0;

            double chunkCountSum = 0.0;
            double tokenSum = 0.0;
            int chunkTotal = 0;
            int docCount = 0;

            foreach (EvalDocument document in documents)
            {
                IReadOnlyList<Chunk> chunks = chunker.Chunk(document.Text, options);
                docCount++;
                chunkCountSum += chunks.Count;
                foreach (Chunk chunk in chunks)
                {
                    tokenSum += chunk.TokenCount;
                    chunkTotal++;
                }

                foreach (EvalSpan span in document.Spans)
                {
                    List<Interval> retrieved = new List<Interval>();
                    foreach (Chunk chunk in chunks)
                    {
                        if (chunk.StartOffset < 0) continue;
                        if (chunk.EndOffset > span.Start && chunk.StartOffset < span.End)
                            retrieved.Add(new Interval(chunk.StartOffset, chunk.EndOffset));
                    }

                    Interval golden = new Interval(span.Start, span.End);
                    int unionRetrieved = Interval.UnionLength(new List<Interval>(retrieved));
                    int intersection = Interval.IntersectionWithLength(retrieved, golden);
                    int union = unionRetrieved + span.Length - intersection;

                    double recall = span.Length > 0 ? (double)intersection / span.Length : 0.0;
                    double precision = unionRetrieved > 0 ? (double)intersection / unionRetrieved : 0.0;
                    double iou = union > 0 ? (double)intersection / union : 0.0;

                    recallSum += recall;
                    precisionSum += precision;
                    iouSum += iou;
                    spanCount++;
                }
            }

            return new ConfigScore
            {
                Name = name,
                Recall = spanCount > 0 ? recallSum / spanCount : 0.0,
                Precision = spanCount > 0 ? precisionSum / spanCount : 0.0,
                Iou = spanCount > 0 ? iouSum / spanCount : 0.0,
                MeanChunks = docCount > 0 ? chunkCountSum / docCount : 0.0,
                MeanTokens = chunkTotal > 0 ? tokenSum / chunkTotal : 0.0
            };
        }
    }
}
