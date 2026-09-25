namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using TextChunker.Enums;
    using TextChunker.Models;
    using TextChunker.Observability;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Regression coverage for span based chunking: cuts never land inside a surrogate pair or grapheme, offsets
    /// always re slice the source, chunks with overlap never repeat a predecessor or leave a redundant tail, chunk
    /// boundaries do not drift on multi line text, and overlap is measured in whole words.
    /// </summary>
    public static class SpanIntegritySuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "SpanIntegrity",
                displayName: "Span Integrity",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("SpanIntegrity", "WordPieceEmojiAfterLineBreaksGrid", "WordPiece chunks emoji text after line breaks across a budget and overlap grid",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string source = "\n\n" + string.Concat(Enumerable.Range(0, 200).Select(i => "Point " + i + " is done! \U0001F426 "));
                            foreach (int maxTokens in new[] { 16, 32, 64, 128, 243 })
                            {
                                foreach (int overlap in new[] { 0, 16, 64 })
                                {
                                    if (overlap >= maxTokens) continue;
                                    ChunkingOptions options = new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = maxTokens, OverlapCount = overlap };
                                    IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, options);
                                    TestSupport.Assert(chunks.Count > 0, "expected chunks at " + maxTokens + "/" + overlap);
                                    TestSupport.AssertSpanIntegrity("grid " + maxTokens + "/" + overlap, source, chunks, maxTokens, true);
                                }
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "WordPieceEveryTextStrategy", "Every text strategy keeps exact offsets on multi line emoji text under WordPiece",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string source = ChatTranscript(60);
                            foreach (ChunkStrategyEnum strategy in new[] { ChunkStrategyEnum.FixedTokenCount, ChunkStrategyEnum.SentenceBased, ChunkStrategyEnum.ParagraphBased, ChunkStrategyEnum.Recursive, ChunkStrategyEnum.ListEntry, ChunkStrategyEnum.WholeList })
                            {
                                ChunkingOptions options = new ChunkingOptions { Strategy = strategy, MaxTokens = 48 };
                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, options);
                                TestSupport.Assert(chunks.Count > 1, strategy + ": expected multiple chunks");
                                TestSupport.AssertSpanIntegrity(strategy.ToString(), source, chunks, 48, true);
                            }

                            IReadOnlyList<Chunk> regex = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { Strategy = ChunkStrategyEnum.RegexBased, RegexPattern = @"\n\n", MaxTokens = 48 });
                            TestSupport.AssertSpanIntegrity("RegexBased", source, regex, 48, false);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "NoRedundantTailWithOverlap", "Overlap never produces a redundant tail, duplicate chunks, or unresolved offsets",
                        executeAsync: ct =>
                        {
                            BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                            string source = string.Concat(Enumerable.Range(0, 400).Select(i => "XYlaunch" + i + " "));
                            IReadOnlyList<Chunk> overlapped = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { MaxTokens = 235, OverlapCount = 16 });
                            IReadOnlyList<Chunk> plain = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { MaxTokens = 235, OverlapCount = 0 });

                            TestSupport.AssertSpanIntegrity("overlap 16", source, overlapped, 235, true);
                            TestSupport.AssertSpanIntegrity("overlap 0", source, plain, 235, true);
                            TestSupport.Assert(overlapped[overlapped.Count - 1].EndOffset == source.TrimEnd().Length, "the last chunk should end at the end of the text");
                            TestSupport.Assert(overlapped.Count <= plain.Count + 2, "overlap produced " + overlapped.Count + " chunks against " + plain.Count + " without overlap");
                            TestSupport.Assert(overlapped.Select(c => c.Text).Distinct(StringComparer.Ordinal).Count() == overlapped.Count, "overlap produced duplicate chunks");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "OverlapSweepStrictlyAdvances", "Starts and ends strictly advance and cover the source across an overlap sweep",
                        executeAsync: ct =>
                        {
                            ITokenizerAdapter[] tokenizers = new ITokenizerAdapter[] { new BertWordPieceTokenizerAdapter(), new SharpTokenTokenizerAdapter("cl100k_base") };
                            string[] sources = new[] { TestSupport.WordCorpus(900), MultiLine(150), ChatTranscript(40) };
                            foreach (ITokenizerAdapter tokenizer in tokenizers)
                            {
                                foreach (string source in sources)
                                {
                                    foreach (int maxTokens in new[] { 32, 100, 243 })
                                    {
                                        foreach (int overlap in new[] { 1, 16, 64 })
                                        {
                                            if (overlap >= maxTokens) continue;
                                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { MaxTokens = maxTokens, OverlapCount = overlap });
                                            TestSupport.AssertSpanIntegrity(tokenizer.GetType().Name + " " + maxTokens + "/" + overlap, source, chunks, maxTokens, true);
                                        }
                                    }
                                }
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "NoDriftOnMultiLineText", "Without overlap, consecutive chunks are contiguous on multi line text",
                        executeAsync: ct =>
                        {
                            string source = MultiLine(200);
                            foreach (ITokenizerAdapter tokenizer in new ITokenizerAdapter[] { new BertWordPieceTokenizerAdapter(), new SharpTokenTokenizerAdapter("cl100k_base") })
                            {
                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { MaxTokens = 64, OverlapCount = 0 });
                                TestSupport.Assert(chunks.Count > 5, "expected many chunks");
                                TestSupport.Assert(chunks[0].StartOffset == 0, "the first chunk should start at the first word");
                                for (int i = 1; i < chunks.Count; i++)
                                {
                                    string gap = source.Substring(chunks[i - 1].EndOffset, chunks[i].StartOffset - chunks[i - 1].EndOffset);
                                    TestSupport.Assert(gap.Trim().Length == 0, tokenizer.GetType().Name + ": text between chunk " + (i - 1) + " and " + i + " was skipped or overlapped: " + gap);
                                }
                                TestSupport.Assert(chunks[chunks.Count - 1].EndOffset == source.TrimEnd().Length, "the last chunk should end at the end of the text");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "OverlapIsWithinOneWord", "The shared region between chunks is at most the overlap and within one word of it",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.WordCorpus(600);
                            foreach (ITokenizerAdapter tokenizer in new ITokenizerAdapter[] { new BertWordPieceTokenizerAdapter(), new SharpTokenTokenizerAdapter("cl100k_base") })
                            {
                                foreach (int overlap in new[] { 8, 16, 32 })
                                {
                                    IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { MaxTokens = 64, OverlapCount = overlap });
                                    for (int i = 1; i < chunks.Count; i++)
                                    {
                                        int shared = chunks[i - 1].EndOffset - chunks[i].StartOffset;
                                        TestSupport.Assert(shared > 0, "expected an overlapping region between chunk " + (i - 1) + " and " + i);
                                        int sharedTokens = tokenizer.CountTokens(source.Substring(chunks[i].StartOffset, shared));
                                        TestSupport.Assert(sharedTokens <= overlap + 1, "overlap of " + sharedTokens + " tokens exceeds the requested " + overlap);
                                        TestSupport.Assert(sharedTokens >= overlap - 4, "overlap of " + sharedTokens + " tokens is more than a word short of " + overlap);
                                    }
                                }
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "OverlapAtOrAboveBudgetTerminates", "Overlap at or above the budget still terminates with strictly advancing chunks",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.WordCorpus(120);
                            foreach (int overlap in new[] { 16, 40, 1000 })
                            {
                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { MaxTokens = 16, OverlapCount = overlap });
                                TestSupport.AssertSpanIntegrity("overlap " + overlap, source, chunks, 16, true);
                                TestSupport.Assert(chunks.Count <= 120, "overlap " + overlap + " produced more chunks than words");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "GraphemesNeverSplit", "An oversized run of emoji and combining marks is cut only at grapheme boundaries",
                        executeAsync: ct =>
                        {
                            string source = string.Concat(Enumerable.Repeat("\U0001F468\u200D\U0001F469\u200D\U0001F467e\u0301\U0001F1FA\U0001F1F8", 30));
                            HashSet<int> boundaries = new HashSet<int>(StringInfo.ParseCombiningCharacters(source)) { source.Length };
                            foreach (ITokenizerAdapter tokenizer in new ITokenizerAdapter[] { new SharpTokenTokenizerAdapter("cl100k_base"), new BertWordPieceTokenizerAdapter() })
                            {
                                IReadOnlyList<Chunk> chunks = TestSupport.Chunk(tokenizer, source, new ChunkingOptions { MaxTokens = 24 });
                                // Under WordPiece the whole run is one word that maps to a single [UNK], exactly as the runtime
                                // counts it, so only the byte pair tokenizer is expected to split it.
                                if (tokenizer is SharpTokenTokenizerAdapter)
                                    TestSupport.Assert(chunks.Count > 1, "expected the oversized run to split");
                                TestSupport.AssertSpanIntegrity(tokenizer.GetType().Name, source, chunks, 24, true);
                                StringBuilder rebuilt = new StringBuilder();
                                foreach (Chunk c in chunks)
                                {
                                    TestSupport.Assert(boundaries.Contains(c.StartOffset) && boundaries.Contains(c.EndOffset), "chunk " + c.Position + " splits a grapheme cluster");
                                    rebuilt.Append(c.Text);
                                }
                                TestSupport.Assert(string.Equals(rebuilt.ToString(), source, StringComparison.Ordinal), "chunks of a run with no whitespace should tile it exactly");
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "EveryTextStrategyReportsOffsets", "Every text strategy reports resolved offsets for every chunk",
                        executeAsync: ct =>
                        {
                            string source = TestSupport.MultiParagraph + "\n\n" + MultiLine(20);
                            foreach (ChunkStrategyEnum strategy in new[] { ChunkStrategyEnum.FixedTokenCount, ChunkStrategyEnum.SentenceBased, ChunkStrategyEnum.ParagraphBased, ChunkStrategyEnum.Recursive, ChunkStrategyEnum.ListEntry, ChunkStrategyEnum.WholeList })
                            {
                                foreach (int overlap in new[] { 0, 2 })
                                {
                                    IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { Strategy = strategy, MaxTokens = 40, OverlapCount = overlap });
                                    TestSupport.AssertSpanIntegrity(strategy + "/" + overlap, source, chunks, 40, true);
                                }
                            }
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "RecursiveKeepsSeparatorContent", "Recursive boundaries keep heading markers and sentence punctuation",
                        executeAsync: ct =>
                        {
                            string markdown = "# Guide\n\nIntro text for the guide.\n## Setup\nInstall the package and configure it carefully.\n## Usage\nCall the chunker with options and read the chunks.";
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(markdown, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, Format = ContentFormatEnum.Markdown, MaxTokens = 14 });
                            TestSupport.AssertSpanIntegrity("markdown", markdown, chunks, 14, true);
                            TestSupport.Assert(chunks.Any(c => c.Text.StartsWith("## Setup", StringComparison.Ordinal)), "a chunk should begin with its heading marker");

                            IReadOnlyList<Chunk> prose = TestSupport.Chunk(TestSupport.MultiParagraph, new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 24 });
                            TestSupport.AssertSpanIntegrity("prose", TestSupport.MultiParagraph, prose, 24, true);
                            foreach (Chunk c in prose)
                                TestSupport.Assert(c.Text.EndsWith(".", StringComparison.Ordinal), "a sentence split chunk lost its period: " + c.Text);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "HierarchyOffsetsAreExact", "Hierarchy aware chunks carry exact offsets, including CRLF input",
                        executeAsync: ct =>
                        {
                            string markdown = "# Guide\r\n\r\nIntro paragraph for the guide.\r\n\r\n## Setup\r\nInstall it. Configure it.\r\n\r\n## Usage\r\nCall it with options.";
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(markdown, new ChunkingOptions { HierarchyAware = true, MaxTokens = 64 });
                            TestSupport.Assert(chunks.Count == 3, "expected one chunk per section, got " + chunks.Count);
                            TestSupport.AssertSpanIntegrity("hierarchy", markdown, chunks, 64, true, false);
                            TestSupport.Assert(chunks[1].HeaderContext == "Guide > Setup", "unexpected breadcrumb " + chunks[1].HeaderContext);

                            IReadOnlyList<Chunk> contextualized = TestSupport.Chunk(markdown, new ChunkingOptions { HierarchyAware = true, ContextualizeHeaders = true, MaxTokens = 64 });
                            foreach (Chunk c in contextualized)
                                TestSupport.Assert(c.StartOffset == -1 && c.EndOffset == -1, "a contextualized chunk is not a source substring and must report -1 offsets");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "SmallChunkMergeKeepsSpans", "Merging small chunks keeps the original separator and exact offsets",
                        executeAsync: ct =>
                        {
                            string source = "Tiny.\n\nAlso tiny.\n\nA considerably longer sentence that carries enough words to stand alone as a chunk.";
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { Strategy = ChunkStrategyEnum.RegexBased, RegexPattern = @"\n\n", MaxTokens = 12, MinChunkTokens = 6, SmallChunkMode = SmallChunkModeEnum.MergeForward });
                            TestSupport.AssertSpanIntegrity("merge", source, chunks, 12, true);
                            TestSupport.Assert(chunks[0].Text.StartsWith("Tiny.\n\nAlso tiny.", StringComparison.Ordinal), "the merged chunk should keep the blank line separator: " + chunks[0].Text);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "SentenceAwareOverlapStartsAtSentences", "Sentence boundary aware overlap starts each following chunk at a sentence",
                        executeAsync: ct =>
                        {
                            StringBuilder builder = new StringBuilder();
                            for (int i = 0; i < 60; i++)
                                builder.Append("Sentence number ").Append(i).Append(" describes a small detail of the story. ");
                            string source = builder.ToString().TrimEnd();
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { MaxTokens = 64, OverlapCount = 16, OverlapStrategy = OverlapStrategyEnum.SentenceBoundaryAware });
                            TestSupport.AssertSpanIntegrity("sentence aware", source, chunks, 64, true);
                            for (int i = 1; i < chunks.Count; i++)
                                TestSupport.Assert(chunks[i].Text.StartsWith("Sentence number", StringComparison.Ordinal), "chunk " + i + " does not start at a sentence: " + chunks[i].Text.Substring(0, 20));
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "SemanticAwareOverlapStartsAtParagraphs", "Semantic boundary aware overlap starts following chunks at paragraphs",
                        executeAsync: ct =>
                        {
                            StringBuilder builder = new StringBuilder();
                            for (int i = 0; i < 30; i++)
                                builder.Append("Paragraph ").Append(i).Append(" opens here. It has a second sentence with some words.\n\n");
                            string source = builder.ToString().TrimEnd();
                            IReadOnlyList<Chunk> chunks = TestSupport.Chunk(source, new ChunkingOptions { MaxTokens = 80, OverlapCount = 20, OverlapStrategy = OverlapStrategyEnum.SemanticBoundaryAware });
                            TestSupport.AssertSpanIntegrity("semantic aware", source, chunks, 80, true);
                            for (int i = 1; i < chunks.Count; i++)
                                TestSupport.Assert(chunks[i].Text.StartsWith("Paragraph ", StringComparison.Ordinal), "chunk " + i + " does not start at a paragraph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SpanIntegrity", "ContainmentBackstopNeverFires", "The containment backstop records no suppressed chunks across a sweep",
                        executeAsync: ct =>
                        {
                            long suppressed = 0;
                            using (MeterListener listener = new MeterListener())
                            {
                                listener.InstrumentPublished = (instrument, l) =>
                                {
                                    if (instrument.Meter.Name == ChunkingMetrics.Name && instrument.Name == "textchunker.chunks_suppressed")
                                        l.EnableMeasurementEvents(instrument);
                                };
                                listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => System.Threading.Interlocked.Add(ref suppressed, value));
                                listener.Start();

                                string source = ChatTranscript(30) + "\n\n" + MultiLine(40);
                                foreach (ChunkStrategyEnum strategy in new[] { ChunkStrategyEnum.FixedTokenCount, ChunkStrategyEnum.SentenceBased, ChunkStrategyEnum.ParagraphBased, ChunkStrategyEnum.Recursive })
                                    foreach (int overlap in new[] { 0, 1, 3, 24 })
                                        TestSupport.Chunk(new BertWordPieceTokenizerAdapter(), source, new ChunkingOptions { Strategy = strategy, MaxTokens = 48, OverlapCount = overlap });
                            }

                            TestSupport.Assert(suppressed == 0, "the backstop suppressed " + suppressed + " chunks");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string MultiLine(int lines)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines; i++)
            {
                builder.Append("line ").Append(i).Append(" has\twords and a caf\u00E9 note");
                builder.Append(i % 5 == 4 ? "\r\n\r\n" : "\n");
            }

            return builder.ToString();
        }

        private static string ChatTranscript(int turns)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < turns; i++)
            {
                builder.Append(i % 2 == 0 ? "User: " : "Assistant: ");
                builder.Append("Turn ").Append(i).Append(" talks about Twitter engagement! \U0001F426 See you soon \U0001F680. ");
                builder.Append("Na\u00EFve r\u00E9sum\u00E9 notes \U0001F468\u200D\U0001F469\u200D\U0001F467 and \u65E5\u672C\u8A9E text follow.\n\n");
            }

            return builder.ToString();
        }
    }
}
