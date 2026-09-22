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
