namespace TextChunker.Exceptions
{
    using System;

    /// <summary>
    /// Raised when a tokenizer or tokenization profile cannot be resolved.
    /// </summary>
    public class TokenizerResolutionException : Exception
    {
        /// <summary>
        /// Initialize a new instance with a descriptive message.
        /// </summary>
        /// <param name="message">Message describing the failure.</param>
        public TokenizerResolutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initialize a new instance with a descriptive message and an inner exception.
        /// </summary>
        /// <param name="message">Message describing the failure.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public TokenizerResolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
