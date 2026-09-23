namespace TextChunker.Chunking
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using TextChunker.Models;

    /// <summary>
    /// Chunks text and structured content into an ordered stream of chunks. Implementations resolve a tokenizer
    /// and token budget once per call and route every input mode through one internal pipeline.
    /// </summary>
    public interface IChunker
    {
        /// <summary>
        /// Chunk a text string.
        /// </summary>
        /// <param name="text">Text to chunk.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An asynchronous stream of chunks.</returns>
        IAsyncEnumerable<Chunk> ChunkText(string text, ChunkingOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Chunk the text content of a stream. The stream is read but not disposed.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="encoding">Text encoding, or null for UTF-8 with byte order mark detection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An asynchronous stream of chunks.</returns>
        IAsyncEnumerable<Chunk> ChunkStream(Stream stream, ChunkingOptions? options = null, Encoding? encoding = null, CancellationToken token = default);

        /// <summary>
        /// Chunk the text content of a file identified by name.
        /// </summary>
        /// <param name="filename">Path to the file.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="fileOptions">File access options, or null for shared read UTF-8.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An asynchronous stream of chunks.</returns>
        IAsyncEnumerable<Chunk> ChunkFile(string filename, ChunkingOptions? options = null, FileChunkingOptions? fileOptions = null, CancellationToken token = default);

        /// <summary>
        /// Chunk a list, driving the WholeList and ListEntry strategies.
        /// </summary>
        /// <param name="items">List items.</param>
        /// <param name="ordered">True for a numbered list, false for a bulleted list.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An asynchronous stream of chunks.</returns>
        IAsyncEnumerable<Chunk> ChunkList(IEnumerable<string> items, bool ordered = false, ChunkingOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Chunk a table, driving the table strategies. Row zero is treated as the header.
        /// </summary>
        /// <param name="rows">Table rows, each a list of cell values.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An asynchronous stream of chunks.</returns>
        IAsyncEnumerable<Chunk> ChunkTable(IReadOnlyList<IReadOnlyList<string>> rows, ChunkingOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Chunk a fully formed structured request, including a hierarchy of child content.
        /// </summary>
        /// <param name="request">Structured request.</param>
        /// <param name="options">Chunking options, or null to use defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An asynchronous stream of chunks.</returns>
        IAsyncEnumerable<Chunk> ChunkRequest(ContentRequest request, ChunkingOptions? options = null, CancellationToken token = default);
    }
}
