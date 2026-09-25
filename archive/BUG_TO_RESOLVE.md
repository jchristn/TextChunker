# Bugs to Resolve: WordPiece Span Chunking (Surrogate Crash, Redundant Tail Chunks, Budget Drift)

Found on 2026-09-24 while benchmarking Isis (C:\Code\AgentMemory) with the `all-minilm` embedding model served by Ollama. Isis chunks memory bodies with TextChunker's `FixedTokenCount` strategy and the `BertWordPiece` tokenizer, and it currently carries three workarounds for what is described here (`C:\Code\AgentMemory\src\Isis.Core\Recall\MemoryChunker.cs:149-154`, `:184-189`, `:221-245`, and `:299-308`). Everything below was re-verified against the TextChunker working tree at commit `8e8c774` with a throwaway console app that references `src\TextChunker\TextChunker.csproj` (Microsoft.ML.Tokenizers 2.0.0, net10.0). Where a claim rests on a measurement rather than on reading code, the section says so.

The three reported symptoms turned out to share a root. `BertWordPieceTokenizerAdapter.SliceByTokenRange` converts token positions to character positions with `Tokenizer.GetIndexByTokenCount`, and that method returns an index into the **normalized** text. The adapter discards the normalized text and applies the index to the original string. Separately, the span loop in `ChunkingHelpers.ChunkByTokenSpans` keeps its bookkeeping in whole-text token positions, but the slices it gets back start at word boundaries that do not match those positions. The first mismatch produces the surrogate crash; the second produces the redundant tail; both feed the unresolved offsets. The budget drift in issue 3 comes from the same normalizer, which does not match what the serving runtime does.

## Issue 1: Cuts land inside UTF-16 surrogate pairs and crash the tokenizer

### Summary

Chunking text that contains an emoji (or any astral-plane character) can throw `System.ArgumentException: String contains invalid Unicode code points. (Parameter 'strInput')`. The cut is not placed inside the pair by the span arithmetic itself: `GetIndexByTokenCount` returns a correct index for the normalized string, and the normalized string is shorter or longer than the original, so the index lands somewhere else in the original. When that somewhere else is between a high and a low surrogate, the next tokenizer call receives a string that starts with a lone low surrogate and ICU normalization rejects it.

### Severity

**High.** The exception aborts the whole `Chunk` call, so the caller gets no chunks at all for that document. In Isis that meant the memory could not be stored. The trigger is ordinary chat text: a line break anywhere before an emoji is enough. The drift that causes it also misplaces every cut in multi-line text even when nothing crashes (see Root cause).

### Reproduction

The synthetic string in the original report (`"\U0001F680launch0 \U0001F600launch1 ..."`) does **not** throw. It contains no characters that normalization adds or removes, so the index drift is zero and every cut is correct. Adding leading line breaks is enough to make it fail:

```csharp
using TextChunker.Chunking;
using TextChunker.Enums;
using TextChunker.Models;
using TextChunker.Tokenization;

BertWordPieceTokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();

// Smallest direct reproduction: the start index for token 10 is 45 in normalized coordinates,
// which is the low surrogate of the bird emoji in the original string.
string s = "\n\nNow, let's talk about Twitter engagement! \U0001F426 See you soon.";
tokenizer.SliceByTokenRange(s, 10, 5);   // throws ArgumentException

// Through the public API. Throws with OverlapCount = 0 too, so overlap is not required.
string text = "\n\n" + string.Concat(Enumerable.Range(0, 200).Select(i => "Point " + i + " is done! \U0001F426 "));
new Chunker(tokenizer).Chunk(text, new ChunkingOptions
{
    Strategy = ChunkStrategyEnum.FixedTokenCount,
    MaxTokens = 64,
    OverlapCount = 0
});   // throws ArgumentException

// Same text without the leading "\n\n": 16 chunks, no exception.
```

Measured results for the `"\n\n" + ...` text: it throws at `MaxTokens`/`OverlapCount` of 16/0, 32/0, 64/0, 100/0, 128/0, 128/64, 243/64, and more. Without the leading line breaks it throws at none of the combinations tried.

On real data, a scan of the 2,803 unique sessions in the Isis LongMemEval 60-question corpus (`C:\Code\AgentMemory\benchmarks\data\longmemeval-s-60.json`, bodies formatted as Isis ingests them) found 19 sessions containing surrogate pairs. Two of them threw: `89cce497` at `MaxTokens` 240 and 254, and `8376624e` at 235 and 240, all with `OverlapCount` 64. The stack matches the one reported from Isis exactly:

```
System.ArgumentException: String contains invalid Unicode code points. (Parameter 'strInput')
   at System.Globalization.Normalization.IcuNormalize(String strInput, NormalizationForm normalizationForm)
   at Microsoft.ML.Tokenizers.BertNormalizer.Normalize(String original)
   at Microsoft.ML.Tokenizers.WordPieceTokenizer.GetIndexByTokenCount(...)
   at TextChunker.Chunking.ChunkingHelpers.ChunkByTokenSpans(...) in ChunkingHelpers.cs:line 39
   at TextChunker.Chunking.ChunkDispatcher.Produce(...) in ChunkDispatcher.cs:line 30
```

I could not locate the specific 4,591-character session with 36 emoji from the original report in this corpus, so that exact input is unverified. The mechanism is the same for any input that fails.

### Root cause

`src\TextChunker\Tokenization\BertWordPieceTokenizerAdapter.cs:41-71`:

```csharp
int startCharIndex = 0;
if (startTokenIndex > 0)
{
    startCharIndex = _Tokenizer.Value.GetIndexByTokenCount(
        text.AsSpan(),
        startTokenIndex,
        out _,          // normalizedText, discarded
        out _);
}

if (startCharIndex >= text.Length) return string.Empty;

ReadOnlySpan<char> remaining = text.AsSpan(startCharIndex);   // index applied to the ORIGINAL text
int lengthCharIndex = _Tokenizer.Value.GetIndexByTokenCount(
    remaining,
    tokenCount,
    out _,
    out _);
```

The Microsoft.ML.Tokenizers 2.0.0 XML documentation for `Tokenizer.GetIndexByTokenCount(ReadOnlySpan<char>, int, out string, out int, bool, bool)` says the return value is "the length of the input text or the `normalizedText` if the normalization is enabled", and that `normalizedText` is set "if the tokenizer's normalization is enabled". The BERT tokenizer is created with normalization on (`LowerCaseBeforeTokenization = true`, `ApplyBasicTokenization = true`, lines 81-87), so both indices above are in normalized coordinates.

The normalizer changes length in several ways I measured directly by reading `normalizedText` back:

| Input | Original length | Normalized length | What changed |
|---|---|---|---|
| `"alpha\n\nbeta gamma\tdelta\r\nepsilon"` | 32 | 27 | `\n`, `\t`, `\r` deleted outright (words fuse: `alphabeta`) |
| `"\n\nNow, let's talk ... \U0001F426 "` | 47 | 45 | leading `\n\n` deleted |
| `"cafe\u0301 au lait"` | 13 | 12 | combining mark composed (NFC) |
| `"\u65E5\u672C\u8A9E text"` (CJK) | 8 | 14 | spaces inserted around each CJK character |
| `"plain words here"` | 16 | 16 | none |

In session `89cce497` the normalized text is 192 characters shorter than the original, all from deleted line breaks and tabs. By the end of that document, every computed start position is up to 192 characters earlier than intended. A crash needs the drifted index to land exactly on a low surrogate, which is why only 2 of 19 emoji-bearing sessions failed, but the drift itself affects every multi-line document.

The adapter is reached from `ChunkingHelpers.CreateStrictTokenSlice` (`src\TextChunker\Chunking\ChunkingHelpers.cs:232-261`, call at line 244), which `ChunkByTokenSpans` calls at line 39. `ChunkByTokenSpans` backs `FixedTokenCount` (`src\TextChunker\Chunkers\FixedTokenChunker.cs:12-15`), the overflow path of `WholeList` (`src\TextChunker\Chunking\ChunkDispatcher.cs:138`), oversized lines in `ListEntry` (line 146), and oversized units for the unit-packing strategies (`ChunkingHelpers.cs:197`, `:208`, `:213`). The sentence and paragraph boundary adjusters (`ChunkingHelpers.cs:263-287`) call `SliceByTokenRange(text, 0, tokenPosition)` and inherit the same drift. With my sample input, `Recursive`, `SentenceBased`, and `ParagraphBased` did not throw, so those paths are exposed in principle but not demonstrated.

The existing Unicode coverage misses the bug because its emoji input (`src\Test.Shared\Suites\UnicodeSuite.cs:24`) has no line breaks, and the emoji test at lines 39-42 runs the `Recursive` strategy with the default tokenizer rather than `BertWordPiece` with `FixedTokenCount`.

The BPE adapters do not use `GetIndexByTokenCount`. `SharpTokenTokenizerAdapter.SliceByTokenRange` (lines 48-60) and `MlTokenizerAdapter.SliceByTokenRange` (lines 49-61) decode an id range instead. A cl100k run over emoji-dense text produced no lone surrogates, no U+FFFD, and no unresolved offsets, so I consider them unaffected by this issue, though decoding a partial byte-level range could in theory yield U+FFFD and is worth a test.

### Proposed fix

Snapping cut positions to surrogate boundaries (moving an index that sits on a low surrogate back by one) stops the crash and is worth doing as a guard in `SliceByTokenRange` regardless. It does not fix the bug, because the cut is still up to hundreds of characters away from where the token arithmetic intended it.

The real fix is to stop applying normalized-coordinate indices to the original string. Two workable designs:

1. **Own the pre-tokenization (recommended).** Split the original text into word units with their original start and end offsets (a whitespace split keeps surrogate pairs whole by construction, since emoji are not whitespace), count each unit with `CountTokens`, and do the span arithmetic over units and character positions. WordPiece tokenization is word-local after basic tokenization, so summed unit counts are the count of the text once line breaks are treated as whitespace. Cuts always land on unit boundaries in the original text, and offsets come for free. A single unit larger than the budget still needs splitting; do that with a surrogate-safe character loop that grows or shrinks a substring and re-counts it.
2. **Map normalized offsets back.** Keep `GetIndexByTokenCount`, capture `normalizedText`, and build an original-to-normalized offset map by replaying the normalizer's edits. The normalizer is not ours, so the map would have to track Microsoft.ML.Tokenizers behavior release by release. I would not choose this.

One tempting shortcut does not work: calling `GetIndexByTokenCount(..., considerPreTokenization: true, considerNormalization: false)` on a pre-lower-cased string. With the BERT tokenizer, disabling normalization made the whole input a single pre-token (the call returned the full length with a token count of 1 for an eight-word sentence), so the index is useless.

I prototyped design 1 in the throwaway app and ran it over the same 2,803 sessions at `MaxTokens` 243 and overlap 64: zero exceptions and zero chunks starting or ending on a lone surrogate. The chunk-count results for that run are under Issue 2.

### Tests to add

- `\n\n` + emoji-dense text through `FixedTokenCount` with `BertWordPiece`, across a grid of `MaxTokens` (16, 32, 64, 128, 243) and `OverlapCount` (0, 16, 64): no exception, every chunk a valid UTF-16 string (no unpaired surrogate at either end), every chunk within budget.
- The direct adapter case: `SliceByTokenRange("\n\nNow, let's talk about Twitter engagement! \U0001F426 See you soon.", 10, 5)` does not throw and returns a string that is a substring of the input.
- A property test in `FuzzSuite`: random strings mixing ASCII words, `\n`, `\t`, `\r\n`, emoji, ZWJ sequences, combining marks, and CJK. For every chunk, `input.Substring(StartOffset, EndOffset - StartOffset) == chunk.Text` and the chunk has no lone surrogate.
- Drift regression: in a multi-line text of 200 paragraphs, the first chunk with overlap 0 ends exactly where the second begins (contiguous coverage), which fails today because starts drift earlier.

## Issue 2: Redundant trailing chunks and unresolved offsets with overlap

### Summary

With `OverlapCount > 0`, `FixedTokenCount` often emits a run of short chunks at the end of a document, each wholly contained in the chunk before it, and several of them identical. Identical chunks after the first get `StartOffset = -1` and `EndOffset = -1`.

### Severity

**Medium.** No content is lost, but every redundant chunk costs an embedding call and a stored vector, and short tail fragments crowd search results because their embeddings are dominated by a few words. The `-1` offsets break any caller that slices the source by offset (Isis had to fall back to `LastIndexOf`).

### Reproduction

```csharp
string text = string.Concat(Enumerable.Range(0, 400).Select(i => "XYlaunch" + i + " "));
IReadOnlyList<Chunk> chunks = new Chunker(new BertWordPieceTokenizerAdapter()).Chunk(text, new ChunkingOptions
{
    Strategy = ChunkStrategyEnum.FixedTokenCount,
    MaxTokens = 235,
    OverlapCount = 16,
    ComputeOffsets = true
});
// 18 chunks. Lengths: 506, 472, 450, 467, 467, 467, 467, 467, 467, 467, 299, 35, 23, 23, 23, 23, 23, 23
// Chunks 11-16 are each contained in their predecessor; chunks 13-16 have StartOffset -1.
// With OverlapCount = 0 the same text yields 10 contiguous chunks and no -1 offsets.
```

The chunk count, the tail shape, and the four `-1` offsets match the original report. Two lengths differ slightly (450 where the report had 462, and 299 where it had 287); the report may have been produced from a slightly different build, and the difference does not change the behavior.

On the 60-question LongMemEval corpus (2,803 sessions, `MaxTokens` 243, `OverlapCount` 64, no header) the current code produced 43,425 chunks. Of those, 4,378 (10.1%) were wholly contained in their predecessor, 1,882 were under 200 characters, and 677 had `StartOffset = -1`. The original report's figures (474 sessions, 7,836 chunks, 720 removable, 64 unresolved offsets) came from a different subset that I did not re-run, but the rates line up. The word-unit prototype from Issue 1 produced 36,501 chunks for the same corpus (16% fewer) with 85 under 200 characters. It also had 79 contained chunks and 4 over budget, both artifacts of the prototype not splitting single oversized units, which the real fix must handle.

### Root cause

`src\TextChunker\Chunking\ChunkingHelpers.cs:20-67`:

```csharp
int totalTokens = tokenizer.CountTokens(text);                                        // line 28
...
while (position < totalTokens)
{
    int requestedTokenCount = Math.Min(tokenLimit, totalTokens - position);           // line 38
    TokenSlice slice = CreateStrictTokenSlice(text, position, requestedTokenCount, tokenizer, tokenLimit);
    if (slice.TokenCount <= 0) break;
    if (!string.IsNullOrWhiteSpace(slice.Text)) chunks.Add(slice.Text);

    if (position + slice.TokenCount >= totalTokens) break;                           // line 45

    int advance = slice.TokenCount - overlapTokens;                                   // line 47
    if (advance <= 0) advance = 1;                                                    // line 48
    ...
    position += advance;                                                              // line 62
}
```

`position` is a whole-text token index, but the slice for that position does not start there. `GetIndexByTokenCount` never splits a word's WordPiece sub-tokens, so when `position` falls inside a word (routine once overlap is subtracted), the start index is rounded back to the start of that word. The slice then begins some tokens before `position`, and asking it for `totalTokens - position` tokens cannot reach the end of the text. The termination test at line 45 compares token counts that no longer describe the same span, so it fails near the end. Once the short remaining slice has fewer tokens than the overlap, line 48 forces `advance = 1`, and `position` creeps forward one token per iteration while the start keeps snapping back to the same word, emitting the same text again each time.

A trace of the loop on the reproduction text shows the pattern exactly:

| Iteration | `position` | Requested | Start char | Tokens before start | Slice tokens | Slice end char |
|---|---|---|---|---|---|---|
| 11 | 2175 | 153 | 4378 | 2172 | 150 | 4678 |
| 12 | 2309 | 19 | 4642 | 2304 | 18 | 4678 |
| 13 | 2311 | 17 | 4654 | 2310 | 12 | 4678 |
| 14 to 17 | 2312 to 2315 | 16 to 13 | 4654 | 2310 | 12 | 4678 |
| 18 | 2316 | 12 | 4666 | 2316 | 12 | 4690 (end) |

Every slice from iteration 11 to 17 stops at character 4678, one word short of the end, and iterations 13 to 17 are the same 23 characters. Only when `position` crosses the word boundary (iteration 18) does the start stop snapping and the slice reach the end. On real multi-line text the normalization drift from Issue 1 makes the starts land even earlier, which is why real sessions produce longer redundant tails than the synthetic case.

The `-1` offsets come from `src\TextChunker\Chunking\Chunker.cs:423-432`:

```csharp
int index = offsetSource.IndexOf(body, Math.Min(searchFrom, offsetSource.Length), StringComparison.Ordinal);
if (index >= 0)
{
    chunk.StartOffset = index;
    chunk.EndOffset = index + body.Length;
    searchFrom = index + 1;
}
```

`searchFrom` starts at 0 (line 290) and becomes `index + 1` after each located chunk. An exact duplicate of the previous chunk starts at `index`, which is before `searchFrom`, so the forward search cannot find it and the chunk keeps the model defaults of -1 (`src\TextChunker\Models\Chunk.cs:58` and `:63`). The report's explanation (tail chunks that start before `searchFrom` cannot be located) is correct; in the reproduction every unresolved chunk is specifically a duplicate that starts at the previous chunk's start. The same search has a quieter failure mode: in repetitive text a chunk can match an earlier occurrence after `searchFrom` and get a wrong but non-negative offset.

### Proposed fix

The word-unit design from Issue 1 fixes this issue too, because it keeps positions in characters and units, terminates when the last unit is consumed, and computes overlap by walking back whole units. If the span loop is kept instead, the minimum fix is:

- Track the character start and end of each emitted slice. Terminate when the slice end reaches the end of the text, instead of comparing token counts at line 45.
- Require each new slice to start at a character position strictly greater than the previous start, and treat a slice whose end equals the previous end as the final chunk (merge or stop, never emit it again).
- Replace the forced `advance = 1` with an advance computed from the slice's actual start, so the loop cannot re-emit the same span.
- Keep a cheap backstop that drops a chunk wholly contained in its predecessor, logged through `ChunkingMetrics` so the rate is visible. Isis does this today at `MemoryChunker.cs:186-189`.

For offsets, carry the start and end computed by the span math on `RawPiece` and use them in `Enrich` instead of `IndexOf`, adjusting for the characters removed by `TrimWhitespace` (`Chunker.cs:312-333`). Where a strategy genuinely cannot supply positions, search from the previous chunk's **start** rather than `start + 1`, which at least places a duplicate at its true location.

### Tests to add

- The reproduction above: no chunk is contained in its predecessor, no chunk has `StartOffset` -1, and the last chunk ends at the end of the trimmed text.
- For `OverlapCount` in 1, 16, 64 and several `MaxTokens` values on long ASCII, multi-line, and LongMemEval-like text: consecutive chunks have strictly increasing `StartOffset` and strictly increasing `EndOffset`, and the union of spans covers the input.
- Overlap accuracy: the token count of the overlapping region between consecutive chunks is within one word of `OverlapCount`, which today is not true once drift sets in.
- Offset integrity for every strategy that marks pieces offset-eligible: `input.Substring(StartOffset, EndOffset - StartOffset)` equals the untrimmed body.

## Issue 3: Local token counts diverge from the serving runtime

### Summary

For `all-minilm`, TextChunker resolves the known-model entry (`src\TextChunker\Tokenization\TokenizationDefaults.cs:235`, 256 tokens with 2 reserved), giving `EffectiveInputBudget` 254. Chunks sized to that budget were still rejected by Ollama's `all-minilm`, whose limit is 256 including `[CLS]` and `[SEP]`. Isis now subtracts a 4% margin (`MemoryChunker.cs:299-308`) and retries on overflow. The original report guessed that `BertNormalizer` strips accents while the runtime does not. Measurement says the opposite: the local tokenizer keeps accents, and Ollama behaves as if it strips them.

### Severity

**Medium.** A chunk that the local tokenizer says fits can be rejected by the model, and the rejection costs a round trip and a re-chunk. The undercount is worst on accented and emoji-heavy text, which is the text least likely to appear in English-only tests.

### What was verified

Counts below are from the local `BertWordPieceTokenizerAdapter` and from Ollama `all-minilm:latest` via `/api/embed` (`prompt_eval_count`, minus 2 for `[CLS]` and `[SEP]`; the plain sentence "alpha beta gamma delta epsilon" returns 7, which confirms the 2-token framing):

| Input | Local | Ollama | Local, `RemoveNonSpacingMarks = true` |
|---|---|---|---|
| `Türkçe çok güzel bir dil, şehir ışıkları görünüyor.` | 11 | 23 | 23 |
| Same sentence with accents removed by hand | 21 | 21 | not run |
| `café naïve résumé` | 3 | 3 | 3 |
| `great job 😀 🚀 thanks` | 3 | 5 | not run |
| `α-helix β-sheet 10⁻³ M ± 5%` | 10 | 13 | not run |
| `alpha\n\nbeta gamma\tdelta\r\nepsilon` | 8 | 5 | not run |

Three separate divergences show up:

- **Accents.** The adapter does not set `BertOptions.RemoveNonSpacingMarks` (`BertWordPieceTokenizerAdapter.cs:81-87`), so accented words reach an uncased vocabulary that was built without accents and become a single `[UNK]` each (`[UNK] [UNK] [UNK] bi ##r dil , ...`). Ollama splits them into sub-words. Setting `RemoveNonSpacingMarks = true` reproduced Ollama's count exactly on the Turkish sample. For `café` both sides happen to produce one token per word, so the table shows no gap there.
- **Emoji and some symbols.** The local tokenizer emits no token at all for standalone emoji and for characters such as `⁻`, `³`, and `±`; Ollama counts at least one token for each. That undercount is consistent with the "up to about 7 more tokens on scientific text" Isis measured, but I only confirmed it on the samples above.
- **Line breaks and tabs.** The local normalizer deletes `\n`, `\t`, and `\r` (see the Issue 1 table), fusing words across the break into longer, more fragmented tokens. Ollama treats them as whitespace. The direction of the error depends on the words, so this one can overcount as well as undercount.

What remains hypothesis: why Ollama behaves this way. The results are consistent with Ollama following the Hugging Face BERT `BasicTokenizer` defaults for an uncased model (accent stripping follows lower-casing, control whitespace becomes a space, unknown characters become `[UNK]`), but I did not read Ollama's tokenizer source, and other runtimes (Text Embeddings Inference, vLLM, sentence-transformers directly) were not measured.

### Proposed fix

Fixing parity where it is cheap and exposing a margin for the rest seems like the right split:

- **Normalization parity for uncased WordPiece profiles.** Set `RemoveNonSpacingMarks = true` when the profile is `bert-base-uncased`, and replace `\n`, `\t`, `\r` with spaces before tokenizing (a length-preserving substitution, so it does not introduce new offset drift). Both changes alter token counts for existing callers and belong in the CHANGELOG as behavior changes. Check whether `nomic-embed-text` (also mapped to `bert-base-uncased`) gains or loses from the same change before applying it to every uncased profile.
- **Count unknown symbols.** If the tokenizer drops a non-whitespace character entirely, count it as one `[UNK]` in `CountTokens` for budget purposes. That is conservative and matches the measured runtime.
- **Configurable safety margin.** Add an optional margin on the profile (percentage or absolute tokens) that `EffectiveInputBudget` subtracts after `ReservedInputTokens`, defaulting to zero so existing results do not move. Callers who cannot risk a rejection can set it; Isis would set it instead of hard-coding 4%.
- **Documentation.** `docs\TOKENIZERS.md` should say plainly that local counts are an approximation of the runtime's, list the divergences above, and recommend setting `EffectiveInputBudget` from the runtime's documented limit plus handling an overflow response. The existing `ITokenizerCalibrationProbe` measures the budget as a single number, so it cannot detect per-text divergence; saying so avoids false confidence.

### Tests to add

- Golden counts for the table's inputs under the chosen normalization, stored in `src\Test.Shared\Fixtures` so a future Microsoft.ML.Tokenizers upgrade that changes behavior fails loudly.
- `CountTokens("a\nb") == CountTokens("a b")` for the uncased profile once line breaks are mapped to spaces.
- A budget property test: for random accented, emoji, and symbol-heavy text, a chunk the library emits within budget still fits when counted with the parity rules (a proxy for "the runtime accepts it" that runs offline).

## Documentation and CHANGELOG notes

Record under the next release heading in `CHANGELOG.md`, without changing any version numbers here:

- Fixed: WordPiece chunking no longer throws `ArgumentException` on text that contains emoji or other astral characters after line breaks. Cut positions were computed in normalized-text coordinates and applied to the original text.
- Fixed: fixed-token chunking with overlap no longer emits repeated tail chunks, and every emitted chunk has resolved offsets.
- Changed: chunk boundaries for multi-line WordPiece input move, because cuts are now placed where the token arithmetic intends rather than drifting earlier through the document.
- Changed (if adopted): uncased WordPiece profiles strip accents and treat line breaks and tabs as whitespace when counting, which raises counts on accented text and changes counts on multi-line text.
- Added (if adopted): an optional token safety margin on the tokenization profile.

`docs\STRATEGIES.md` should state the overlap guarantee precisely (consecutive chunks share about `OverlapCount` tokens, rounded to whole words). `docs\TOKENIZERS.md` gets the runtime-divergence section described above.

## Checklist

Mark status with `[ ]` open, `[~]` in progress, `[x]` done, `[-]` dropped. Fill in owner and notes as work lands.

| # | Task | Status | Owner | Notes |
|---|---|---|---|---|
| 1 | Guard: never return a start or end index on a low surrogate from `SliceByTokenRange` | [x] | Claude | 0.3.0: all adapters return exact substrings that never split a pair; the chunker no longer slices by token index |
| 2 | Choose design: word-unit spans (recommended) or normalized-offset map | [x] | Claude | Word-unit spans, generalized to every tokenizer and strategy |
| 3 | Implement character and unit bookkeeping in `ChunkByTokenSpans` | [x] | Claude | `ChunkingHelpers.ChunkByTokenWindow` over `WordSegmenter` units |
| 4 | Surrogate-safe splitting for a single unit larger than the budget | [x] | Claude | Grapheme cluster boundaries, code point fallback |
| 5 | Terminate on character end of text; strictly advancing starts; no forced `advance = 1` | [x] | Claude | Superseded by the span loop, which guarantees strictly increasing starts and ends |
| 6 | Backstop: drop chunks contained in their predecessor, with a metric | [x] | Claude | `textchunker.chunks_suppressed`; a test asserts it stays 0 |
| 7 | Carry offsets on `RawPiece`; stop using `IndexOf` in `Enrich` | [x] | Claude | All text strategies, hierarchy chunks, and small-chunk merges |
| 8 | Review sentence and paragraph boundary adjusters for the same drift | [x] | Claude | Removed; boundary-aware overlap reimplemented on spans |
| 9 | Tests: emoji after line breaks grid, direct adapter case, fuzz with offset integrity | [x] | Claude | `SpanIntegritySuite`, `WordPieceSuite`, `FuzzSuite` |
| 10 | Tests: no contained chunks, strictly increasing offsets, overlap accuracy | [x] | Claude | `SpanIntegritySuite` |
| 11 | Test: BPE partial-range decode never yields U+FFFD or lone surrogates | [x] | Claude | It did yield U+FFFD before; fixed and pinned in `TokenizerSuite` |
| 12 | Decide on `RemoveNonSpacingMarks` and line-break mapping for uncased profiles | [x] | Claude | HF-faithful WordPiece port; nomic-embed-text counts matched all-minilm on every sample |
| 13 | Count dropped non-whitespace characters as `[UNK]` for budgeting | [x] | Claude | Covered by the port: unsegmentable words become `[UNK]` |
| 14 | Optional profile safety margin, default zero | [x] | Claude | `SafetyMarginTokens` and `SafetyMarginPercentage` on `ChunkingOptions` |
| 15 | Golden token-count fixtures for accented, emoji, symbol, multi-line input | [x] | Claude | `Fixtures/wordpiece-counts.json`, 43 inputs measured against Ollama |
| 16 | CHANGELOG, `docs\STRATEGIES.md`, `docs\TOKENIZERS.md` | [x] | Claude | 0.3.0 |
| 17 | Tell Isis when fixed so it can remove its stand-in, dedupe, and margin workarounds | [ ] | | `MemoryChunker.cs:149-154`, `:186-189`, `:299-308` |
