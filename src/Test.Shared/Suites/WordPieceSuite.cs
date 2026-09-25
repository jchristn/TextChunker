namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the BERT WordPiece tokenizer: counts that match the embedding runtime (a fixture measured against
    /// Ollama all-minilm), the normalization rules behind them, offsets that always index the original text, the
    /// configurable options, a caller supplied vocabulary, and argument validation.
    /// </summary>
    public static class WordPieceSuite
    {
        private const string _ResourceName = "Test.Shared.Fixtures.wordpiece-counts.json";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "WordPiece",
                displayName: "WordPiece Tokenizer",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("WordPiece", "GoldenCountsMatchRuntime", "Counts match the golden fixture and never fall below the runtime count",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            List<WordPieceGoldenCount> cases = LoadGolden();
                            TestSupport.Assert(cases.Count >= 40, "golden fixture unexpectedly small: " + cases.Count);
                            int exact = 0;
                            foreach (WordPieceGoldenCount golden in cases)
                            {
                                int actual = tokenizer.CountTokens(golden.Text);
                                TestSupport.Assert(actual == golden.Count, golden.Category + ": count " + actual + " != golden " + golden.Count + " for " + JsonSerializer.Serialize(golden.Text));
                                TestSupport.Assert(actual >= golden.RuntimeCount, golden.Category + ": count " + actual + " is below the runtime count " + golden.RuntimeCount);
                                if (actual == golden.RuntimeCount) exact++;
                            }

                            TestSupport.Assert(exact >= cases.Count - 1, "expected all but at most one case to match the runtime exactly, matched " + exact + " of " + cases.Count);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "LineBreaksAreWhitespace", "Line breaks, tabs, and other whitespace separate words instead of fusing them",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            int spaced = tokenizer.CountTokens("alpha beta");
                            foreach (string separator in new[] { "\n", "\r\n", "\t", "\u000B", "\u000C", "\u00A0", "\u2028", "\n\n\n" })
                                TestSupport.Assert(tokenizer.CountTokens("alpha" + separator + "beta") == spaced, "separator " + JsonSerializer.Serialize(separator) + " did not behave as whitespace");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "SymbolsAreCounted", "Emoji, symbols, and astral characters cost at least one token each",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            foreach (string symbol in new[] { "\U0001F600", "$", "+", "=", "<", "|", "~", "^", "\u00A9", "\u20AC", "\u00B1", "\u00BD", "\u2192", "\U0001D401" })
                                TestSupport.Assert(tokenizer.CountTokens(symbol) >= 1, "symbol " + JsonSerializer.Serialize(symbol) + " counted as zero tokens");
                            TestSupport.Assert(tokenizer.CountTokens("\u200B\u00AD\uFEFF") == 0, "zero width format characters should be removed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "AccentStrippingIsConfigurable", "Accents are stripped by default and kept when disabled",
                        executeAsync: ct =>
                        {
                            string turkish = "T\u00FCrk\u00E7e \u00E7ok g\u00FCzel";
                            BertWordPieceTokenizerAdapter stripped = new BertWordPieceTokenizerAdapter();
                            BertWordPieceTokenizerAdapter kept = new BertWordPieceTokenizerAdapter(new WordPieceOptions { StripAccents = false });
                            TestSupport.Assert(stripped.CountTokens(turkish) == stripped.CountTokens("Turkce cok guzel"), "stripped count should equal the unaccented count");
                            TestSupport.Assert(kept.CountTokens(turkish) != stripped.CountTokens(turkish), "keeping accents should change the count on an uncased vocabulary");
                            TestSupport.Assert(stripped.Options.StripAccents && !kept.Options.StripAccents, "options should report the configured value");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "LongWordLimitIsConfigurable", "Long words are segmented by default and collapse to one unknown token under a limit",
                        executeAsync: ct =>
                        {
                            string word = new string('a', 150);
                            BertWordPieceTokenizerAdapter unlimited = new BertWordPieceTokenizerAdapter();
                            BertWordPieceTokenizerAdapter limited = new BertWordPieceTokenizerAdapter(new WordPieceOptions { MaxInputCharactersPerWord = 100 });
                            TestSupport.Assert(unlimited.CountTokens(word) > 50, "an unlimited long word should be segmented into many pieces");
                            TestSupport.Assert(limited.CountTokens(word) == 1, "a limited long word should become a single unknown token");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "EncodeDecodeRoundTrip", "Encode agrees with CountTokens and Decode reassembles continuation pieces",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string text = "Tokenization of embeddings is unaffable";
                            IReadOnlyList<int> ids = tokenizer.Encode(text);
                            TestSupport.Assert(ids.Count == tokenizer.CountTokens(text), "encode length should equal the count");
                            TestSupport.Assert(string.Equals(tokenizer.Decode(ids), text.ToLowerInvariant(), StringComparison.Ordinal), "decode should reassemble the lower cased words: " + tokenizer.Decode(ids));
                            TestSupport.Assert(tokenizer.Encode(string.Empty).Count == 0, "empty input should encode to nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "SliceIsAlwaysASubstring", "Every token range slice is a substring of the input with no lone surrogate",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string text = "\n\nNow, let's talk about Twitter engagement! \U0001F426 See you soon.\r\n\tcaf\u00E9 \U0001F468\u200D\U0001F469\u200D\U0001F467 \u65E5\u672C\u8A9E e\u0301 $5 + 3";
                            int total = tokenizer.CountTokens(text);
                            for (int start = 0; start < total; start++)
                            {
                                for (int count = 1; count <= 6; count++)
                                {
                                    string slice = tokenizer.SliceByTokenRange(text, start, count);
                                    TestSupport.Assert(slice.Length > 0, "slice " + start + "+" + count + " was empty");
                                    TestSupport.Assert(text.IndexOf(slice, StringComparison.Ordinal) >= 0, "slice " + start + "+" + count + " is not a substring");
                                    TestSupport.Assert(!TestSupport.HasLoneSurrogate(slice), "slice " + start + "+" + count + " holds a lone surrogate");
                                }
                            }

                            TestSupport.Assert(tokenizer.SliceByTokenRange(text, total, 5).Length == 0, "a slice past the end should be empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "ReportedCrashInputSlices", "The reported crash input slices without throwing",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string text = "\n\nNow, let's talk about Twitter engagement! \U0001F426 See you soon.";
                            string slice = tokenizer.SliceByTokenRange(text, 10, 5);
                            TestSupport.Assert(text.IndexOf(slice, StringComparison.Ordinal) >= 0, "slice should be a substring of the input");
                            TestSupport.Assert(slice.StartsWith("\U0001F426", StringComparison.Ordinal), "token 10 is the bird emoji, got " + JsonSerializer.Serialize(slice));
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "CustomVocabularyStream", "A caller supplied vocabulary is honored, including a cased configuration",
                        executeAsync: ct =>
                        {
                            string vocabulary = "[PAD]\n[UNK]\nhello\nHello\nworld\n##s\n!\n";
                            BertWordPieceTokenizerAdapter uncased = new BertWordPieceTokenizerAdapter(new MemoryStream(Encoding.UTF8.GetBytes(vocabulary)));
                            BertWordPieceTokenizerAdapter cased = new BertWordPieceTokenizerAdapter(
                                new MemoryStream(Encoding.UTF8.GetBytes(vocabulary)),
                                new WordPieceOptions { LowerCase = false, StripAccents = false });

                            TestSupport.Assert(uncased.CountTokens("Hello worlds!") == 4, "uncased should lower case and split worlds into world ##s");
                            TestSupport.Assert(string.Equals(uncased.Decode(uncased.Encode("Hello worlds!")), "hello worlds !", StringComparison.Ordinal), "uncased decode mismatch");
                            TestSupport.Assert(string.Equals(cased.Decode(cased.Encode("Hello worlds!")), "Hello worlds !", StringComparison.Ordinal), "cased decode should keep the capital");
                            TestSupport.Assert(cased.Encode("Goodbye")[0] == 1, "an unknown word should map to the [UNK] identifier");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WordPiece", "ConcurrentCountsAreStable", "Concurrent counting on a shared adapter is deterministic",
                        executeAsync: async ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string text = string.Concat(System.Linq.Enumerable.Repeat("Caf\u00E9 \U0001F600 \u65E5\u672C naive ", 200));
                            int expected = tokenizer.CountTokens(text);
                            List<Task<int>> tasks = new List<Task<int>>();
                            for (int i = 0; i < 16; i++)
                                tasks.Add(Task.Run(() => tokenizer.CountTokens(text), ct));
                            int[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
                            foreach (int result in results)
                                TestSupport.Assert(result == expected, "concurrent count " + result + " != " + expected);
                        }),

                    new TestCaseDescriptor("WordPiece", "RejectsInvalidArguments", "Constructors, options, and methods reject invalid arguments",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            TestSupport.ExpectThrows<ArgumentNullException>(() => new BertWordPieceTokenizerAdapter((WordPieceOptions)null!), "null options should be rejected");
                            TestSupport.ExpectThrows<ArgumentNullException>(() => new BertWordPieceTokenizerAdapter((Stream)null!), "null vocabulary should be rejected");
                            TestSupport.ExpectThrows<InvalidDataException>(() => new BertWordPieceTokenizerAdapter(new MemoryStream(new byte[0])), "an empty vocabulary should be rejected");
                            TestSupport.ExpectThrows<ArgumentException>(() => new BertWordPieceTokenizerAdapter(new MemoryStream(Encoding.UTF8.GetBytes("hello\nworld\n"))), "a vocabulary without [UNK] should be rejected");
                            TestSupport.ExpectThrows<ArgumentOutOfRangeException>(() => new WordPieceOptions { MaxInputCharactersPerWord = -1 }, "a negative word limit should be rejected");
                            TestSupport.ExpectThrows<ArgumentNullException>(() => new WordPieceOptions { UnknownToken = " " }, "a blank unknown token should be rejected");
                            TestSupport.ExpectThrows<ArgumentNullException>(() => tokenizer.Decode(null!), "null ids should be rejected");
                            TestSupport.ExpectThrows<ArgumentOutOfRangeException>(() => tokenizer.Decode(new[] { -1 }), "a negative id should be rejected");
                            TestSupport.ExpectThrows<ArgumentOutOfRangeException>(() => tokenizer.Decode(new[] { 999999 }), "an out of range id should be rejected");
                            TestSupport.ExpectThrows<ArgumentOutOfRangeException>(() => tokenizer.SliceByTokenRange("hello", -1, 1), "a negative start should be rejected");
                            TestSupport.Assert(tokenizer.CountTokens(null!) == 0, "null text should count as zero");
                            TestSupport.Assert(tokenizer.SliceByTokenRange("hello", 0, 0).Length == 0, "a zero count slice should be empty");
                            return Task.CompletedTask;
                        })
                });
        }

        private static List<WordPieceGoldenCount> LoadGolden()
        {
            Assembly assembly = typeof(WordPieceSuite).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream(_ResourceName))
            {
                if (stream == null) throw new Exception("embedded golden fixture not found: " + _ResourceName);
                using (StreamReader reader = new StreamReader(stream))
                {
                    JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    List<WordPieceGoldenCount>? cases = JsonSerializer.Deserialize<List<WordPieceGoldenCount>>(reader.ReadToEnd(), options);
                    return cases ?? new List<WordPieceGoldenCount>();
                }
            }
        }
    }
}
