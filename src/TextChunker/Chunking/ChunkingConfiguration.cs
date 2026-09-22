namespace TextChunker.Chunking
{
    using System;
    using TextChunker.Enums;

    /// <summary>
    /// Internal per operation configuration consumed by the chunking strategies. The public entry point is
    /// ChunkingOptions, which the chunker maps onto this type after tokenizer and budget resolution.
    /// </summary>
    internal class ChunkingConfiguration
    {
        private int _FixedTokenCount = 256;
        private int _OverlapCount = 0;
        private double? _OverlapPercentage = null;
        private int? _OverlapCharacters = null;
        private int _RowGroupSize = 5;
        private int _RegexTimeoutMilliseconds = 5000;

        internal ChunkStrategyEnum Strategy { get; set; } = ChunkStrategyEnum.FixedTokenCount;

        internal int FixedTokenCount
        {
            get => _FixedTokenCount;
            set => _FixedTokenCount = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(FixedTokenCount), "FixedTokenCount must be at least 1.");
        }

        internal int OverlapCount
        {
            get => _OverlapCount;
            set => _OverlapCount = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(OverlapCount), "OverlapCount must be at least 0.");
        }

        internal double? OverlapPercentage
        {
            get => _OverlapPercentage;
            set
            {
                if (value.HasValue && (value.Value < 0.0 || value.Value > 1.0))
                    throw new ArgumentOutOfRangeException(nameof(OverlapPercentage), "OverlapPercentage must be between 0.0 and 1.0.");
                _OverlapPercentage = value;
            }
        }

        internal int? OverlapCharacters
        {
            get => _OverlapCharacters;
            set
            {
                if (value.HasValue && value.Value < 0)
                    throw new ArgumentOutOfRangeException(nameof(OverlapCharacters), "OverlapCharacters must be at least 0.");
                _OverlapCharacters = value;
            }
        }

        internal OverlapStrategyEnum OverlapStrategy { get; set; } = OverlapStrategyEnum.SlidingWindow;

        internal System.Collections.Generic.List<string>? Separators { get; set; } = null;

        internal int RowGroupSize
        {
            get => _RowGroupSize;
            set => _RowGroupSize = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(RowGroupSize), "RowGroupSize must be at least 1.");
        }

        internal string? ContextPrefix { get; set; } = null;

        internal string? RegexPattern { get; set; } = null;

        internal int RegexTimeoutMilliseconds
        {
            get => _RegexTimeoutMilliseconds;
            set => _RegexTimeoutMilliseconds = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(RegexTimeoutMilliseconds), "RegexTimeoutMilliseconds must be at least 1.");
        }
    }
}
