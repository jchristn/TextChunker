namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TextChunker.Chunking;
    using TextChunker.Models;

    /// <summary>
    /// Shared helpers for the test suites. No console output.
    /// </summary>
    public static class TestSupport
    {
        /// <summary>
        /// A multi paragraph sample used across suites.
        /// </summary>
        public const string MultiParagraph =
            "The quick brown fox jumps over the lazy dog. The dog was not amused by the fox. "
            + "It had been sleeping soundly for hours before the interruption occurred.\n\n"
            + "Meanwhile, a second paragraph describes an entirely different scene. Rain fell on the quiet town. "
            + "Streets emptied as people retreated indoors to wait out the storm.\n\n"
            + "A final paragraph closes the sample. Chunking must preserve every meaningful word. "
            + "Determinism matters more than anything else in this pipeline.";

        /// <summary>
        /// Build a numeric word corpus of the given length, for example "word1 word2 word3".
        /// </summary>
        /// <param name="count">Number of words.</param>
        /// <returns>Space joined corpus.</returns>
        public static string WordCorpus(int count)
        {
            return string.Join(" ", Enumerable.Range(1, count).Select(i => "word" + i));
        }

        /// <summary>
        /// Chunk text synchronously using a fresh default chunker.
        /// </summary>
        /// <param name="text">Text to chunk.</param>
        /// <param name="options">Options.</param>
        /// <returns>Produced chunks.</returns>
        public static IReadOnlyList<Chunk> Chunk(string text, ChunkingOptions options)
        {
            Chunker chunker = new Chunker();
            return chunker.Chunk(text, options);
        }

        /// <summary>
        /// Collect an asynchronous chunk stream into a list synchronously.
        /// </summary>
        /// <param name="source">Chunk stream.</param>
        /// <returns>Materialized chunks.</returns>
        public static IReadOnlyList<Chunk> Collect(System.Collections.Generic.IAsyncEnumerable<Chunk> source)
        {
            return CollectAsync(source).GetAwaiter().GetResult();
        }

        private static async System.Threading.Tasks.Task<List<Chunk>> CollectAsync(System.Collections.Generic.IAsyncEnumerable<Chunk> source)
        {
            List<Chunk> list = new List<Chunk>();
            await foreach (Chunk chunk in source)
                list.Add(chunk);
            return list;
        }

        /// <summary>
        /// Throw when the condition is false.
        /// </summary>
        /// <param name="condition">Condition that must hold.</param>
        /// <param name="message">Failure message.</param>
        /// <exception cref="Exception">Thrown when the condition is false.</exception>
        public static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        /// <summary>
        /// Assert that the action throws an exception of the given type.
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <param name="message">Failure message.</param>
        public static void ExpectThrows<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new Exception(message + " (expected " + typeof(TException).Name + ", got " + ex.GetType().Name + ")");
            }

            throw new Exception(message + " (no exception thrown)");
        }

        /// <summary>
        /// Assert that enumerating the chunk stream throws an exception of the given type.
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="stream">Chunk stream to enumerate.</param>
        /// <param name="message">Failure message.</param>
        public static void ExpectThrowsOnEnumerate<TException>(System.Collections.Generic.IAsyncEnumerable<Chunk> stream, string message) where TException : Exception
        {
            ExpectThrows<TException>(() => Collect(stream), message);
        }
    }
}
