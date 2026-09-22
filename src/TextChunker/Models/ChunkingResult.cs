namespace TextChunker.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// A materialized chunking result: every produced chunk plus run level diagnostics. Returned by callers who
    /// want the whole picture at once rather than streaming.
    /// </summary>
    public class ChunkingResult
    {
        private List<Chunk> _Chunks = new List<Chunk>();
        private ChunkDiagnostic _Diagnostic = new ChunkDiagnostic();

        /// <summary>
        /// Produced chunks in order. Never null.
        /// </summary>
        public List<Chunk> Chunks
        {
            get => _Chunks;
            set => _Chunks = value ?? new List<Chunk>();
        }

        /// <summary>
        /// Run level diagnostics for the operation. Never null.
        /// </summary>
        public ChunkDiagnostic Diagnostic
        {
            get => _Diagnostic;
            set => _Diagnostic = value ?? new ChunkDiagnostic();
        }
    }
}
