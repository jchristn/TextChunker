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
        private const string _Alphabet = "abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ .,;:!?\n\t0123456789 é ñ 中 ";

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
                        })
                });
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
