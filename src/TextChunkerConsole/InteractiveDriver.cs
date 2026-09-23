namespace TextChunkerConsole
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TextChunker.Chunking;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Serialization;

    /// <summary>
    /// Interactive driver for the TextChunker library. Lets a user pick an input, configure options, and watch
    /// chunks stream out.
    /// </summary>
    internal sealed class InteractiveDriver
    {
        private readonly IChunker _Chunker = new Chunker();

        internal async Task<int> RunAsync()
        {
            Console.WriteLine("TextChunker interactive console");
            Console.WriteLine("Type a menu number and press Enter. Choose 0 to quit.");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("Input source:");
                Console.WriteLine("  1) Sample prose");
                Console.WriteLine("  2) Sample markdown (for hierarchy)");
                Console.WriteLine("  3) Long word list (200 words)");
                Console.WriteLine("  4) Sample list");
                Console.WriteLine("  5) Sample table");
                Console.WriteLine("  6) Type or paste text");
                Console.WriteLine("  7) Load a file");
                Console.WriteLine("  0) Quit");
                Console.Write("> ");

                string? choice = Console.ReadLine();
                if (choice == null || choice.Trim() == "0")
                {
                    Console.WriteLine("Goodbye.");
                    return 0;
                }

                try
                {
                    await HandleChoiceAsync(choice.Trim()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }

                Console.WriteLine();
            }
        }

        private async Task HandleChoiceAsync(string choice)
        {
            switch (choice)
            {
                case "1":
                    await RunTextAsync(SampleCorpora.Prose(), ContentTypeEnum.Text).ConfigureAwait(false);
                    break;
                case "2":
                    await RunTextAsync(SampleCorpora.Markdown(), ContentTypeEnum.Text).ConfigureAwait(false);
                    break;
                case "3":
                    await RunTextAsync(SampleCorpora.LongWordList(), ContentTypeEnum.Text).ConfigureAwait(false);
                    break;
                case "4":
                    await RunListAsync(SampleCorpora.ListItems()).ConfigureAwait(false);
                    break;
                case "5":
                    await RunTableAsync(SampleCorpora.Table()).ConfigureAwait(false);
                    break;
                case "6":
                    Console.Write("Enter text: ");
                    string? typed = Console.ReadLine();
                    if (!string.IsNullOrEmpty(typed)) await RunTextAsync(typed, ContentTypeEnum.Text).ConfigureAwait(false);
                    break;
                case "7":
                    Console.Write("File path: ");
                    string? path = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(path)) await RunFileAsync(path.Trim()).ConfigureAwait(false);
                    break;
                default:
                    Console.WriteLine("Unknown choice.");
                    break;
            }
        }

        private async Task RunTextAsync(string text, ContentTypeEnum inputType)
        {
            ChunkingOptions options = PromptTextOptions();
            options.InputType = inputType;
            await StreamAsync(_Chunker.ChunkText(text, options), text).ConfigureAwait(false);
        }

        private async Task RunFileAsync(string path)
        {
            ChunkingOptions options = PromptTextOptions();
            await StreamAsync(_Chunker.ChunkFile(path, options), null).ConfigureAwait(false);
        }

        private async Task RunListAsync(List<string> items)
        {
            ChunkingOptions options = new ChunkingOptions
            {
                Strategy = PromptStrategy(ChunkStrategyEnum.ListEntry),
                MaxTokens = PromptInt("Max tokens", 128)
            };
            await StreamAsync(_Chunker.ChunkList(items, false, options), null).ConfigureAwait(false);
        }

        private async Task RunTableAsync(List<List<string>> table)
        {
            ChunkingOptions options = new ChunkingOptions
            {
                Strategy = PromptStrategy(ChunkStrategyEnum.RowWithHeaders),
                MaxTokens = PromptInt("Max tokens", 128)
            };
            List<IReadOnlyList<string>> rows = table.Select(r => (IReadOnlyList<string>)r).ToList();
            await StreamAsync(_Chunker.ChunkTable(rows, options), null).ConfigureAwait(false);
        }

        private ChunkingOptions PromptTextOptions()
        {
            ChunkingOptions options = new ChunkingOptions
            {
                Strategy = PromptStrategy(ChunkStrategyEnum.FixedTokenCount),
                MaxTokens = PromptInt("Max tokens", 128),
                OverlapCount = PromptInt("Overlap tokens", 0),
                HierarchyAware = PromptBool("Hierarchy aware", false),
                ComputeHashes = PromptBool("Compute hashes", false)
            };

            string? model = PromptString("Model id (blank for cl100k default)");
            if (!string.IsNullOrWhiteSpace(model)) options.ModelId = model.Trim();

            return options;
        }

        private async Task StreamAsync(IAsyncEnumerable<Chunk> stream, string? source)
        {
            Console.WriteLine();
            Console.WriteLine(string.Format("{0,-4} {1,-6} {2,-6} {3,-14} {4}", "Pos", "Tok", "Chars", "Offsets", "Preview"));
            Console.WriteLine(new string('-', 90));

            List<Chunk> collected = new List<Chunk>();
            Stopwatch stopwatch = Stopwatch.StartNew();

            await foreach (Chunk chunk in stream.ConfigureAwait(false))
            {
                collected.Add(chunk);
                string offsets = chunk.StartOffset >= 0 ? "(" + chunk.StartOffset + "," + chunk.EndOffset + ")" : "(n/a)";
                string preview = Flatten(chunk.HeaderContext != null ? "[" + chunk.HeaderContext + "] " + chunk.Text : chunk.Text);
                Console.WriteLine(string.Format("{0,-4} {1,-6} {2,-6} {3,-14} {4}",
                    chunk.Position, chunk.TokenCount, chunk.CharacterCount, offsets, preview));
            }

            stopwatch.Stop();
            Console.WriteLine(new string('-', 90));
            if (collected.Count == 0)
            {
                Console.WriteLine("No chunks produced.");
                return;
            }

            int totalTokens = collected.Sum(c => c.TokenCount);
            int maxTokens = collected.Max(c => c.TokenCount);
            Console.WriteLine(string.Format(
                "Chunks: {0}   Total tokens: {1}   Mean: {2:0.0}   Max: {3}   Elapsed: {4}ms",
                collected.Count, totalTokens, (double)totalTokens / collected.Count, maxTokens, stopwatch.ElapsedMilliseconds));

            if (PromptBool("Export to JSON file", false))
            {
                Console.Write("Output path: ");
                string? outPath = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(outPath))
                {
                    File.WriteAllText(outPath.Trim(), ChunkerJson.Serialize(collected));
                    Console.WriteLine("Wrote " + collected.Count + " chunks to " + outPath.Trim());
                }
            }
        }

        private static string Flatten(string text)
        {
            string flattened = text.Replace("\r", " ").Replace("\n", " ").Trim();
            const int maxLength = 56;
            return flattened.Length <= maxLength ? flattened : flattened.Substring(0, maxLength) + "...";
        }

        private static ChunkStrategyEnum PromptStrategy(ChunkStrategyEnum fallback)
        {
            Console.WriteLine("Strategy (blank for " + fallback + "):");
            ChunkStrategyEnum[] values = (ChunkStrategyEnum[])Enum.GetValues(typeof(ChunkStrategyEnum));
            for (int i = 0; i < values.Length; i++)
                Console.WriteLine("  " + i + ") " + values[i]);
            Console.Write("> ");

            string? line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return fallback;
            if (int.TryParse(line.Trim(), out int index) && index >= 0 && index < values.Length)
                return values[index];
            return fallback;
        }

        private static int PromptInt(string label, int fallback)
        {
            Console.Write(label + " (blank for " + fallback + "): ");
            string? line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return fallback;
            return int.TryParse(line.Trim(), out int value) ? value : fallback;
        }

        private static bool PromptBool(string label, bool fallback)
        {
            Console.Write(label + " (y/n, blank for " + (fallback ? "y" : "n") + "): ");
            string? line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return fallback;
            string trimmed = line.Trim().ToLowerInvariant();
            return trimmed == "y" || trimmed == "yes" || trimmed == "true";
        }

        private static string? PromptString(string label)
        {
            Console.Write(label + ": ");
            return Console.ReadLine();
        }
    }
}
