namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.ML.Tokenizers;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the tokenizer adapters count, slice, and round trip correctly, and that the strict slice guard
    /// keeps every produced slice within budget.
    /// </summary>
    public static class TokenizerSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Tokenizer",
                displayName: "Tokenizer Adapters",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Tokenizer", "Cl100kCounts", "cl100k adapter counts and round trips",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter("cl100k_base");
                            int count = tokenizer.CountTokens("Hello world, this is a test.");
                            TestSupport.Assert(count > 0, "expected positive token count");
                            string slice = tokenizer.SliceByTokenRange("Hello world, this is a test.", 0, 3);
                            TestSupport.Assert(!string.IsNullOrEmpty(slice), "expected non empty slice");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "WordPieceLoadsEmbeddedVocab", "WordPiece adapter loads the embedded vocab and counts",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            int count = tokenizer.CountTokens("embeddings and tokenization are fun");
                            TestSupport.Assert(count > 0, "expected positive WordPiece token count");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "StrictSliceNeverExceedsBudget", "Every token slice re encodes within the requested budget",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter("cl100k_base");
                            string text = TestSupport.WordCorpus(120);
                            int total = tokenizer.CountTokens(text);
                            for (int start = 0; start < total; start += 8)
                            {
                                string slice = tokenizer.SliceByTokenRange(text, start, 16);
                                if (string.IsNullOrEmpty(slice)) continue;
                                TestSupport.Assert(tokenizer.CountTokens(slice) <= 16, "slice exceeded requested token count");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "EmptyInputReturnsZero", "Empty input returns zero tokens",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter cl = new SharpTokenTokenizerAdapter("cl100k_base");
                            ITokenizerAdapter bert = new BertWordPieceTokenizerAdapter();
                            TestSupport.Assert(cl.CountTokens(string.Empty) == 0, "cl100k empty should be 0");
                            TestSupport.Assert(bert.CountTokens(string.Empty) == 0, "wordpiece empty should be 0");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "O200kCounts", "o200k adapter counts and round trips",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter("o200k_base");
                            int count = tokenizer.CountTokens("Hello world, this is a GPT-4o tokenizer test.");
                            TestSupport.Assert(count > 0, "expected positive o200k token count");
                            string slice = tokenizer.SliceByTokenRange("Hello world, this is a GPT-4o tokenizer test.", 0, 4);
                            TestSupport.Assert(!string.IsNullOrEmpty(slice), "expected non empty o200k slice");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "ReferenceCounts", "Tokenizers match known reference token counts",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter cl = new SharpTokenTokenizerAdapter("cl100k_base");
                            ITokenizerAdapter o2 = new SharpTokenTokenizerAdapter("o200k_base");
                            ITokenizerAdapter wp = new BertWordPieceTokenizerAdapter();

                            // Well-known reference values. "hello world" is a standard two-token cl100k example.
                            TestSupport.Assert(cl.CountTokens("hello") == 1, "cl100k 'hello' should be 1 token");
                            TestSupport.Assert(cl.CountTokens("hello world") == 2, "cl100k 'hello world' should be 2 tokens");
                            TestSupport.Assert(cl.CountTokens("The quick brown fox") == 4, "cl100k 'The quick brown fox' should be 4 tokens");

                            TestSupport.Assert(o2.CountTokens("hello world") == 2, "o200k 'hello world' should be 2 tokens");
                            TestSupport.Assert(o2.CountTokens("The quick brown fox") == 4, "o200k 'The quick brown fox' should be 4 tokens");

                            TestSupport.Assert(wp.CountTokens("hello world") == 2, "wordpiece 'hello world' should be 2 tokens");
                            TestSupport.Assert(wp.CountTokens("The quick brown fox") == 4, "wordpiece 'The quick brown fox' should be 4 tokens");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "BytePairSlicesAreSubstrings", "Byte pair slices over emoji text are substrings with no U+FFFD or lone surrogate",
                        executeAsync: ct =>
                        {
                            string text = "Launch \U0001F680 now! \U0001F468\u200D\U0001F469\u200D\U0001F467 caf\u00E9 \u65E5\u672C\u8A9E \U0001F1FA\U0001F1F8 \U0001F600\U0001F600 done.";
                            foreach (string encoding in new[] { "cl100k_base", "o200k_base" })
                            {
                                ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter(encoding);
                                int total = tokenizer.CountTokens(text);
                                for (int start = 0; start < total; start++)
                                {
                                    for (int count = 1; count <= 4; count++)
                                    {
                                        string slice = tokenizer.SliceByTokenRange(text, start, count);
                                        TestSupport.Assert(slice.IndexOf('\uFFFD') < 0, encoding + " slice " + start + "+" + count + " holds U+FFFD");
                                        TestSupport.Assert(!TestSupport.HasLoneSurrogate(slice), encoding + " slice " + start + "+" + count + " holds a lone surrogate");
                                        TestSupport.Assert(text.IndexOf(slice, StringComparison.Ordinal) >= 0, encoding + " slice " + start + "+" + count + " is not a substring");
                                    }
                                }

                                foreach (int size in new[] { 1, 2, 3, 5 })
                                {
                                    System.Text.StringBuilder tiled = new System.Text.StringBuilder();
                                    for (int start = 0; start < total; start += size)
                                        tiled.Append(tokenizer.SliceByTokenRange(text, start, size));
                                    TestSupport.Assert(string.Equals(tiled.ToString(), text, StringComparison.Ordinal), encoding + " consecutive slices of " + size + " did not tile the text exactly");
                                }

                                TestSupport.ExpectThrows<ArgumentOutOfRangeException>(() => tokenizer.SliceByTokenRange(text, -1, 1), "a negative start should be rejected");
                                TestSupport.Assert(tokenizer.SliceByTokenRange(text, total, 3).Length == 0, "a slice past the end should be empty");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "MlAdapterNormalizingSliceHasNoReplacement", "The ML.Tokenizers adapter never returns U+FFFD from a normalizing tokenizer",
                        executeAsync: ct =>
                        {
                            Assembly assembly = typeof(BertWordPieceTokenizerAdapter).Assembly;
                            using Stream? stream = assembly.GetManifestResourceStream("TextChunker.Tokenization.Data.bert-base-uncased-vocab.txt");
                            TestSupport.Assert(stream != null, "embedded vocab stream not found");
                            Tokenizer bert = BertTokenizer.Create(stream!, new BertOptions { LowerCaseBeforeTokenization = true, ApplyBasicTokenization = true });
                            ITokenizerAdapter adapter = new MlTokenizerAdapter(bert);
                            string text = "Caf\u00E9 launch \U0001F680 now, the \u65E5\u672C team said.";
                            int total = adapter.CountTokens(text);
                            for (int start = 0; start < total; start++)
                            {
                                string slice = adapter.SliceByTokenRange(text, start, 2);
                                TestSupport.Assert(slice.IndexOf('\uFFFD') < 0, "slice " + start + " holds U+FFFD");
                                TestSupport.Assert(!TestSupport.HasLoneSurrogate(slice), "slice " + start + " holds a lone surrogate");
                            }

                            TestSupport.ExpectThrows<ArgumentOutOfRangeException>(() => adapter.SliceByTokenRange(text, -1, 1), "a negative start should be rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tokenizer", "MlAdapterWrapsTokenizer", "The ML.Tokenizers passthrough adapter counts and slices",
                        executeAsync: ct =>
                        {
                            Assembly assembly = typeof(BertWordPieceTokenizerAdapter).Assembly;
                            using Stream? stream = assembly.GetManifestResourceStream("TextChunker.Tokenization.Data.bert-base-uncased-vocab.txt");
                            TestSupport.Assert(stream != null, "embedded vocab stream not found");
                            Tokenizer bert = BertTokenizer.Create(stream!, new BertOptions { LowerCaseBeforeTokenization = true, ApplyBasicTokenization = true });
                            ITokenizerAdapter adapter = new MlTokenizerAdapter(bert);
                            int count = adapter.CountTokens("the passthrough adapter wraps any tokenizer");
                            TestSupport.Assert(count > 0, "expected positive count from ML adapter");
                            string slice = adapter.SliceByTokenRange("the passthrough adapter wraps any tokenizer", 0, 3);
                            TestSupport.Assert(!string.IsNullOrEmpty(slice), "expected non empty slice from ML adapter");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
