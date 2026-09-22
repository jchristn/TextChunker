using System.Collections.Generic;
using System.Linq;
using TextChunker.Enums;
using TextChunker.Models;
using TextChunker.Evaluation;

List<EvalDocument> corpus = EvaluationCorpus.Build();

List<KeyValuePair<string, ChunkingOptions>> configs = new List<KeyValuePair<string, ChunkingOptions>>
{
    Config("Fixed        max=128 ov=0",  ChunkStrategyEnum.FixedTokenCount, 128, 0),
    Config("Fixed        max=128 ov=32", ChunkStrategyEnum.FixedTokenCount, 128, 32),
    Config("Fixed        max=256 ov=0",  ChunkStrategyEnum.FixedTokenCount, 256, 0),
    Config("Sentence     max=128 ov=0",  ChunkStrategyEnum.SentenceBased,   128, 0),
    Config("Sentence     max=64  ov=0",  ChunkStrategyEnum.SentenceBased,   64,  0),
    Config("Paragraph    max=256 ov=0",  ChunkStrategyEnum.ParagraphBased,  256, 0),
    Config("Recursive    max=128 ov=0",  ChunkStrategyEnum.Recursive,       128, 0),
    Config("Recursive    max=64  ov=0",  ChunkStrategyEnum.Recursive,       64,  0),
    Config("Recursive    max=128 ov=16", ChunkStrategyEnum.Recursive,       128, 16)
};

List<ConfigScore> scores = configs
    .Select(c => Evaluator.Evaluate(c.Key, corpus, c.Value))
    .OrderByDescending(s => s.Iou)
    .ToList();

System.Console.WriteLine("TextChunker retrieval-quality evaluation");
System.Console.WriteLine("Oracle retrieval over golden answer spans. Higher recall, precision, and IoU are better.");
System.Console.WriteLine();
System.Console.WriteLine(string.Format("{0,-26} {1,8} {2,10} {3,8} {4,8} {5,9}",
    "Config", "Recall", "Precision", "IoU", "Chunks", "MeanTok"));
System.Console.WriteLine(new string('-', 78));

foreach (ConfigScore score in scores)
{
    System.Console.WriteLine(string.Format("{0,-26} {1,8:0.000} {2,10:0.000} {3,8:0.000} {4,8:0.0} {5,9:0.0}",
        score.Name, score.Recall, score.Precision, score.Iou, score.MeanChunks, score.MeanTokens));
}

System.Console.WriteLine();
ConfigScore best = scores[0];
System.Console.WriteLine("Best by IoU: " + best.Name.Trim() + " (IoU " + best.Iou.ToString("0.000") + ").");

return 0;

static KeyValuePair<string, ChunkingOptions> Config(string name, ChunkStrategyEnum strategy, int maxTokens, int overlap)
{
    ChunkingOptions options = new ChunkingOptions
    {
        Strategy = strategy,
        MaxTokens = maxTokens,
        OverlapCount = overlap
    };
    return new KeyValuePair<string, ChunkingOptions>(name, options);
}
