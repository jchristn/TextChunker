namespace TextChunker.Exceptions
{
    using System;

    /// <summary>
    /// Raised when a chunking operation fails for a reason specific to the chunking pipeline.
    /// </summary>
    public class ChunkingException : Exception
    {
        /// <summary>
        /// Initialize a new instance with a descriptive message.
        /// </summary>
        /// <param name="message">Message describing the failure.</param>
        public ChunkingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initialize a new instance with a descriptive message and an inner exception.
        /// </summary>
        /// <param name="message">Message describing the failure.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public ChunkingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
