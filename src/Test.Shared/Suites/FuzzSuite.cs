namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TextChunker.Enums;
    using TextChunker.Models;
    using Touchstone.Core;

    /// <summary>
    /// Property based checks over many randomly generated inputs with a fixed seed, so the run is reproducible.
    /// Every strategy must keep chunks within budget, terminate, and report offsets that re slice the source
    /// exactly.
    /// </summary>
    public static class FuzzSuite
    {
        private const string _Alphabet = "abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ .,;:!?\n\t0123456789 \u00E9 \u00F1 \u4E2D ";

        private static readonly ChunkStrategyEnum[] _Strategies = new[]
        {
            ChunkStrategyEnum.FixedTokenCount,
            ChunkStrategyEnum.SentenceBased,
            ChunkStrategyEnum.ParagraphBased,
            ChunkStrategyEnum.Recursive
        };

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Fuzz",
                displayName: "Property Based Fuzz",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Fuzz", "InvariantsHoldOverRandomInputs", "Invariants hold across 300 random inputs and every strategy",
                        executeAsync: ct =>
                        {
                            Random random = new Random(20260921);
                            for (int iteration = 0; iteration < 300; iteration++)
                            {
                                string source = RandomText(random);
                                ChunkStrategyEnum strategy = _Strategies[random.Next(_Strategies.Length)];
                                int budget = 8 + random.Next(57);

                                ChunkingOptions options = new ChunkingOptions
                                {
                                    Strategy = strategy,
                                    MaxTokens = budget,
                                    OverlapCount = random.Next(budget / 2)
                                };

                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, options);
                                foreach (Chunk c in chunks)
                                {
                                    if (c.TokenCount > budget)
                                        throw new Exception("iteration " + iteration + " strategy " + strategy + " budget " + budget + ": chunk exceeded budget with " + c.TokenCount);

                                    if (c.StartOffset >= 0)
                                    {
                                        string sub = source.Substring(c.StartOffset, c.EndOffset - c.StartOffset);
                                        if (!string.Equals(sub, c.Text, StringComparison.Ordinal))
                                            throw new Exception("iteration " + iteration + ": offset substring did not match chunk text");
                                    }
                                }
                            }
                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fuzz", "UnicodeSpanInvariantsAcrossTokenizers", "Span invariants hold on random Unicode heavy text under WordPiece and cl100k",
                        executeAsync: ct =>
                        {
                            TextChunker.Tokenization.ITokenizerAdapter[] tokenizers = new TextChunker.Tokenization.ITokenizerAdapter[]
                            {
                                new TextChunker.Tokenization.BertWordPieceTokenizerAdapter(),
                                new TextChunker.Tokenization.SharpTokenTokenizerAdapter("cl100k_base")
                            };
                            ChunkStrategyEnum[] strategies = new[]
                            {
                                ChunkStrategyEnum.FixedTokenCount,
                                ChunkStrategyEnum.SentenceBased,
                                ChunkStrategyEnum.ParagraphBased,
                                ChunkStrategyEnum.Recursive,
                                ChunkStrategyEnum.ListEntry
                            };

                            Random random = new Random(20260924);
                            for (int iteration = 0; iteration < 240; iteration++)
                            {
                                string source = RandomUnicodeText(random);
                                TextChunker.Tokenization.ITokenizerAdapter tokenizer = tokenizers[iteration % tokenizers.Length];
                                ChunkStrategyEnum strategy = strategies[random.Next(strategies.Length)];
                                int budget = 8 + random.Next(120);
                                ChunkingOptions options = new ChunkingOptions
                                {
                                    Strategy = strategy,
                                    MaxTokens = budget,
                                    OverlapCount = strategy == ChunkStrategyEnum.FixedTokenCount ? random.Next(budget) : random.Next(3),
                                    TrimWhitespace = random.Next(4) != 0
                                };

                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, options);
                                string label = "iteration " + iteration + " " + tokenizer.GetType().Name + " " + strategy + " " + budget + "/" + options.OverlapCount;
                                TestSupport.AssertSpanIntegrity(label, source, chunks, budget, true);
                            }
                            return System.Threading.Tasks.Task.CompletedTask;
                        })
                });
        }

        private static readonly string[] _UnicodeFragments = new[]
        {
            "word", "Tokenization", "caf\u00E9", "e\u0301", "na\u00EFve", "T\u00FCrk\u00E7e", "\u65E5\u672C\u8A9E", "\u4E2D\u6587\u5B57",
            "\U0001F600", "\U0001F426", "\U0001F468\u200D\U0001F469\u200D\U0001F467", "\U0001F44D\U0001F3FD", "\U0001F1FA\U0001F1F8",
            "\u0928\u092E\u0938\u094D\u0924\u0947", "\u041F\u0440\u0438\u0432\u0435\u0442", "$5", "a+b=c", "\u00B1", "10\u207B\u00B3",
            "https://example.com/a?b=c", "done!", "end.", "why?", "\u201Cquoted\u201D", "\u200B", "x", "42"
        };

        private static readonly string[] _Separators = new[] { " ", " ", " ", "  ", "\n", "\n\n", "\r\n", "\r\n\r\n", "\t", ". ", "! ", "\u00A0" };

        private static string RandomUnicodeText(Random random)
        {
            int pieces = random.Next(400);
            StringBuilder builder = new StringBuilder();
            if (random.Next(2) == 0) builder.Append(_Separators[random.Next(_Separators.Length)]);
            for (int i = 0; i < pieces; i++)
            {
                builder.Append(_UnicodeFragments[random.Next(_UnicodeFragments.Length)]);
                if (random.Next(6) != 0) builder.Append(_Separators[random.Next(_Separators.Length)]);
            }

            return builder.ToString();
        }

        private static string RandomText(Random random)
        {
            int length = random.Next(1500);
            StringBuilder builder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                builder.Append(_Alphabet[random.Next(_Alphabet.Length)]);
            return builder.ToString();
        }
    }
}
