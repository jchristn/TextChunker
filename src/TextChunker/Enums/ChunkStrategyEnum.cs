namespace TextChunker.Enums
{
    /// <summary>
    /// Chunking strategy for splitting content into chunks.
    /// </summary>
    public enum ChunkStrategyEnum
    {
        /// <summary>Split text into fixed token count chunks.</summary>
        FixedTokenCount,
        /// <summary>Split text at sentence boundaries, grouping sentences to fill the token budget.</summary>
        SentenceBased,
        /// <summary>Split text at paragraph boundaries (double newline).</summary>
        ParagraphBased,
        /// <summary>Treat an entire list as a single chunk.</summary>
        WholeList,
        /// <summary>Each list entry becomes its own chunk.</summary>
        ListEntry,
        /// <summary>Each table data row as space separated values (no headers).</summary>
        Row,
        /// <summary>Each table data row as a markdown table with headers prepended.</summary>
        RowWithHeaders,
        /// <summary>Groups of N table rows with headers prepended (configurable via RowGroupSize).</summary>
        RowGroupWithHeaders,
        /// <summary>Each table row as key value pairs, for example "col1: val1, col2: val2".</summary>
        KeyValuePairs,
        /// <summary>Entire table as a single markdown table chunk.</summary>
        WholeTable,
        /// <summary>Split at boundaries defined by a user supplied regular expression.</summary>
        RegexBased,
        /// <summary>Recursively split on a configurable ladder of separators, then merge to fill the budget.</summary>
        Recursive
    }
}
