namespace TextChunker.Chunking
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TextChunker.Chunkers;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Tokenization;

    /// <summary>
    /// Routes a request to the appropriate strategy and returns intermediate pieces. This is the single production
    /// path shared by every public entry point so behavior cannot diverge between text, stream, file, list, table,
    /// and structured requests.
    /// </summary>
    internal static class ChunkDispatcher
    {
        internal static List<RawPiece> Produce(
            ContentRequest request,
            ChunkingOptions options,
            ITokenizerAdapter tokenizer,
            int workingBudget)
        {
            ChunkingConfiguration config = BuildConfig(options, workingBudget);

            if (options.HierarchyAware && IsTextType(request.Type))
                return ProduceHierarchy(request.Text ?? string.Empty, config, tokenizer, workingBudget, options);

            bool offsetEligible = IsTextType(request.Type);
            List<string> raw = GetRawChunks(request, config, tokenizer, workingBudget);

            List<RawPiece> pieces = new List<RawPiece>(raw.Count);
            foreach (string text in raw)
                pieces.Add(new RawPiece(text, null, offsetEligible));

            return pieces;
        }

        internal static ChunkingConfiguration BuildConfig(ChunkingOptions options, int workingBudget)
        {
            List<string> separators = options.Separators != null && options.Separators.Count > 0
                ? options.Separators
                : SeparatorSets.For(options.Format);

            return new ChunkingConfiguration
            {
                Strategy = options.Strategy,
                FixedTokenCount = Math.Max(1, workingBudget),
                OverlapCount = options.OverlapCount,
                OverlapPercentage = options.OverlapPercentage,
                OverlapCharacters = options.OverlapCharacters,
                OverlapStrategy = options.OverlapStrategy,
                RowGroupSize = options.RowGroupSize,
                ContextPrefix = options.ContextPrefix,
                RegexPattern = options.RegexPattern,
                RegexTimeoutMilliseconds = options.RegexTimeoutMilliseconds,
                Separators = separators
            };
        }

        internal static bool IsTextType(ContentTypeEnum type)
        {
            switch (type)
            {
                case ContentTypeEnum.List:
                case ContentTypeEnum.Table:
                    return false;
                default:
                    return true;
            }
        }

        private static List<RawPiece> ProduceHierarchy(
            string text,
            ChunkingConfiguration config,
            ITokenizerAdapter tokenizer,
            int workingBudget,
            ChunkingOptions options)
        {
            HierarchyNode root = HierarchyBuilder.Build(text);
            List<RawPiece> pieces = new List<RawPiece>();

            foreach (HierarchyNode node in HierarchyBuilder.Flatten(root))
            {
                string content = node.GetContent();
                if (string.IsNullOrWhiteSpace(content)) continue;

                string breadcrumb = node.BuildBreadcrumb(options.HeaderContextSeparator);
                int breadcrumbTokens = options.ContextualizeHeaders && !string.IsNullOrEmpty(breadcrumb)
                    ? tokenizer.CountTokens(breadcrumb)
                    : 0;
                int sectionBudget = Math.Max(1, workingBudget - breadcrumbTokens);

                List<string> raw = DispatchText(content, config, tokenizer, sectionBudget);
                foreach (string chunk in raw)
                    pieces.Add(new RawPiece(chunk, string.IsNullOrEmpty(breadcrumb) ? null : breadcrumb, false));
            }

            return pieces;
        }

        private static List<string> GetRawChunks(
            ContentRequest request,
            ChunkingConfiguration config,
            ITokenizerAdapter tokenizer,
            int tokenBudget)
        {
            switch (request.Type)
            {
                case ContentTypeEnum.List:
                    return ChunkList(request, config, tokenizer, tokenBudget);
                case ContentTypeEnum.Table:
                    return ChunkTableRequest(request, config, tokenizer, tokenBudget);
                default:
                    return DispatchText(request.Text ?? string.Empty, config, tokenizer, tokenBudget);
            }
        }

        private static List<string> DispatchText(string text, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenBudget)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            switch (config.Strategy)
            {
                case ChunkStrategyEnum.FixedTokenCount:
                    return FixedTokenChunker.Chunk(text, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.SentenceBased:
                    return SentenceChunker.Chunk(text, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.ParagraphBased:
                    return ParagraphChunker.Chunk(text, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.RegexBased:
                    return RegexChunker.Chunk(text, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.Recursive:
                    return RecursiveChunker.Chunk(text, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.WholeList:
                    if (tokenizer.CountTokens(text) <= tokenBudget)
                        return new List<string> { text };
                    return ChunkingHelpers.ChunkByTokenSpans(text, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.ListEntry:
                    return ChunkingHelpers.ChunkUnits(
                        text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList(),
                        "\n",
                        tokenBudget,
                        tokenizer,
                        0,
                        line => ChunkingHelpers.ChunkByTokenSpans(line, config, tokenizer, tokenBudget));
                default:
                    return FixedTokenChunker.Chunk(text, config, tokenizer, tokenBudget);
            }
        }

        private static List<string> ChunkList(ContentRequest request, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenBudget)
        {
            List<string>? items = request.OrderedList ?? request.UnorderedList;
            if (items == null || items.Count == 0) return new List<string>();

            bool ordered = request.OrderedList != null;

            switch (config.Strategy)
            {
                case ChunkStrategyEnum.WholeList:
                    return WholeListChunker.Chunk(items, config, ordered, tokenizer, tokenBudget);
                case ChunkStrategyEnum.ListEntry:
                    return ListEntryChunker.Chunk(items, config, tokenizer, tokenBudget);
                default:
                    string serialized = SerializeList(items, ordered);
                    return DispatchText(serialized, config, tokenizer, tokenBudget);
            }
        }

        private static List<string> ChunkTableRequest(ContentRequest request, ChunkingConfiguration config, ITokenizerAdapter tokenizer, int tokenBudget)
        {
            if (request.Table == null || request.Table.Count == 0) return new List<string>();

            switch (config.Strategy)
            {
                case ChunkStrategyEnum.Row:
                    return TableChunker.ChunkByRow(request.Table, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.RowWithHeaders:
                    return TableChunker.ChunkByRowWithHeaders(request.Table, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.RowGroupWithHeaders:
                    return TableChunker.ChunkByRowGroupWithHeaders(request.Table, config.RowGroupSize, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.KeyValuePairs:
                    return TableChunker.ChunkByKeyValuePairs(request.Table, config, tokenizer, tokenBudget);
                case ChunkStrategyEnum.WholeTable:
                    return TableChunker.ChunkWholeTable(request.Table, config.RowGroupSize, config, tokenizer, tokenBudget);
                default:
                    string serialized = SerializeTable(request.Table);
                    return DispatchText(serialized, config, tokenizer, tokenBudget);
            }
        }

        private static string SerializeList(List<string> items, bool ordered)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if (ordered)
                    lines.Add((i + 1) + ". " + items[i]);
                else
                    lines.Add("- " + items[i]);
            }
            return string.Join("\n", lines);
        }

        private static string SerializeTable(List<List<string>> table)
        {
            List<string> lines = new List<string>();
            foreach (List<string> row in table)
                lines.Add(string.Join(" | ", row));
            return string.Join("\n", lines);
        }
    }
}
