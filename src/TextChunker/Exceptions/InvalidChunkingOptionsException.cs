namespace TextChunker.Exceptions
{
    using System;

    /// <summary>
    /// Raised when supplied chunking options are invalid or out of range.
    /// </summary>
    public class InvalidChunkingOptionsException : Exception
    {
        /// <summary>
        /// Initialize a new instance with a descriptive message.
        /// </summary>
        /// <param name="message">Message describing the invalid option.</param>
        public InvalidChunkingOptionsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initialize a new instance with a descriptive message and an inner exception.
        /// </summary>
        /// <param name="message">Message describing the invalid option.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public InvalidChunkingOptionsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
