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
        /// Chunk text synchronously using a chunker bound to an explicit tokenizer.
        /// </summary>
        /// <param name="tokenizer">Tokenizer to use.</param>
        /// <param name="text">Text to chunk.</param>
        /// <param name="options">Options.</param>
        /// <returns>Produced chunks.</returns>
        public static IReadOnlyList<Chunk> Chunk(TextChunker.Tokenization.ITokenizerAdapter tokenizer, string text, ChunkingOptions options)
        {
            Chunker chunker = new Chunker(tokenizer);
            return chunker.Chunk(text, options);
        }

        /// <summary>
        /// Determine whether a string holds an unpaired UTF-16 surrogate anywhere.
        /// </summary>
        /// <param name="text">Text to inspect.</param>
        /// <returns>True when a high surrogate is not followed by a low surrogate, or a low surrogate is not preceded
        /// by a high surrogate.</returns>
        public static bool HasLoneSurrogate(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1])) return true;
                    i++;
                }
                else if (char.IsLowSurrogate(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Assert the span invariants every text strategy guarantees: each chunk is within budget, carries offsets
        /// that re slice the source exactly, holds no lone surrogate, and no chunk is contained in its predecessor.
        /// When strictlyIncreasing is set, starts and ends must also strictly increase and the chunks must together
        /// cover every non whitespace character of the source.
        /// </summary>
        /// <param name="label">Context for failure messages.</param>
        /// <param name="source">Source text that was chunked.</param>
        /// <param name="chunks">Produced chunks.</param>
        /// <param name="budget">Token budget.</param>
        /// <param name="strictlyIncreasing">Whether to require strictly increasing offsets and full coverage.</param>
        public static void AssertSpanIntegrity(string label, string source, IReadOnlyList<Chunk> chunks, int budget, bool strictlyIncreasing)
        {
            AssertSpanIntegrity(label, source, chunks, budget, strictlyIncreasing, strictlyIncreasing);
        }

        /// <summary>
        /// Assert the span invariants with independent control over ordering and coverage, for strategies such as
        /// hierarchy aware chunking where heading lines become metadata rather than chunk text.
        /// </summary>
        /// <param name="label">Context for failure messages.</param>
        /// <param name="source">Source text that was chunked.</param>
        /// <param name="chunks">Produced chunks.</param>
        /// <param name="budget">Token budget.</param>
        /// <param name="strictlyIncreasing">Whether to require strictly increasing offsets.</param>
        /// <param name="requireCoverage">Whether every non whitespace source character must be covered.</param>
        public static void AssertSpanIntegrity(string label, string source, IReadOnlyList<Chunk> chunks, int budget, bool strictlyIncreasing, bool requireCoverage)
        {
            Chunk? previous = null;
            bool[] covered = new bool[source.Length];
            foreach (Chunk c in chunks)
            {
                Assert(c.TokenCount <= budget, label + ": chunk " + c.Position + " over budget with " + c.TokenCount);
                Assert(!HasLoneSurrogate(c.Text), label + ": chunk " + c.Position + " holds a lone surrogate");
                Assert(c.Text.IndexOf('\uFFFD') < 0 || source.IndexOf('\uFFFD') >= 0, label + ": chunk " + c.Position + " holds U+FFFD");
                Assert(c.StartOffset >= 0 && c.EndOffset >= c.StartOffset, label + ": chunk " + c.Position + " has unresolved offsets");

                string sub = source.Substring(c.StartOffset, c.EndOffset - c.StartOffset);
                Assert(string.Equals(sub, c.Text, StringComparison.Ordinal), label + ": chunk " + c.Position + " offsets do not re slice the source");

                if (previous != null)
                {
                    bool contained = c.StartOffset >= previous.StartOffset && c.EndOffset <= previous.EndOffset;
                    Assert(!contained, label + ": chunk " + c.Position + " is contained in its predecessor");
                    if (strictlyIncreasing)
                    {
                        Assert(c.StartOffset > previous.StartOffset, label + ": chunk " + c.Position + " start did not advance");
                        Assert(c.EndOffset > previous.EndOffset, label + ": chunk " + c.Position + " end did not advance");
                    }
                }

                for (int i = c.StartOffset; i < c.EndOffset; i++) covered[i] = true;
                previous = c;
            }

            if (requireCoverage)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (!covered[i] && !char.IsWhiteSpace(source[i]))
                        throw new Exception(label + ": source character " + i + " is not covered by any chunk");
                }
            }
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
