namespace TextChunker.Enums
{
    /// <summary>
    /// File sharing mode used when a chunking operation opens a file by name.
    /// </summary>
    public enum FileAccessModeEnum
    {
        /// <summary>Open the file with no sharing. Other processes cannot open it until the read completes.</summary>
        Exclusive,
        /// <summary>Open the file allowing other readers concurrent read access. This is the default.</summary>
        SharedRead,
        /// <summary>Open the file allowing other readers and writers concurrent access.</summary>
        SharedReadWrite
    }
}
