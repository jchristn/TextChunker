# Changelog

All notable changes to TextChunker are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is below 1.0.0, any
release may carry breaking changes.

## [0.3.0] - 2026-09-24

This release replaces the token slicing core. Every text strategy now works on spans of the original source
text, and the BERT WordPiece tokenizer is reimplemented to match what embedding runtimes actually count.
Chunk boundaries move for most inputs, so re chunk and re embed stored content if you depend on stable
boundaries.

### Fixed

- WordPiece chunking no longer throws `ArgumentException: String contains invalid Unicode code points` on
  text that contains emoji or other astral characters after line breaks. Cut positions were computed in
  the tokenizer's normalized text coordinates and applied to the original text, so they drifted by the
  number of characters the normalizer removed and could land between the two halves of a surrogate pair.
- WordPiece chunk boundaries no longer drift earlier through multi line documents. The same coordinate
  mismatch made chunks overlap or cut words in half even with `OverlapCount = 0`.
- Fixed token chunking with overlap no longer emits a run of short, repeated chunks at the end of a
  document, each contained in the one before it. Chunks now have strictly increasing starts and ends.
- Every chunk from a text strategy now has resolved offsets. Offsets are taken from the span that produced
  the chunk instead of searching the source for the chunk text, so duplicate chunks, repeated passages,
  CRLF line endings, and sentence or paragraph chunks joined across different whitespace no longer report
  `-1` or a wrong location. On a CRLF novel, `Recursive` previously reported `-1` for every chunk and
  `SentenceBased` for most of them.
- WordPiece token counts now match the embedding runtime. Measured against Ollama `all-minilm` over 43
  inputs covering every normalization rule, 42 match exactly and the remaining one counts higher, never
  lower. Previously accented words became single `[UNK]` tokens, line breaks and tabs were deleted (fusing
  the words around them), and emoji, currency and math symbols, and ASCII characters such as `$ + = < > |`
  were dropped entirely, so chunks sized to the budget could be rejected by the model.
- `SharpTokenTokenizerAdapter.SliceByTokenRange` and `MlTokenizerAdapter.SliceByTokenRange` no longer return
  U+FFFD or a lone surrogate when a token range starts or ends inside a multi byte character. The
  SharpToken adapter now returns an exact substring of the input, and consecutive slices tile the text.
- `Recursive` no longer drops separator content at chunk boundaries. A heading marker or keyword that opens
  a block (`\n## `, `\nclass `) stays with the following chunk, and sentence punctuation (`. `) stays with
  the preceding chunk; only the separator's whitespace is discarded.

### Changed

- **Breaking (behavior):** the token window (`FixedTokenCount`, and the fallback every other strategy uses
  for an oversized unit) cuts on word boundaries instead of mid word. A word that alone exceeds the budget
  is split at grapheme cluster boundaries, so a cut never separates a surrogate pair, an emoji ZWJ sequence,
  or a base character from its combining marks. Overlap is measured in whole words and never exceeds
  `OverlapCount` tokens.
- **Breaking (behavior):** `SentenceBased`, `ParagraphBased`, `Recursive`, and `ListEntry` chunks keep the
  original text between their units (for example the blank line between paragraphs) instead of re joining
  units with a fixed separator, so every chunk is an exact substring of the source.
- **Breaking (behavior):** `BertWordPieceTokenizerAdapter` is now a faithful port of the Hugging Face BERT
  basic tokenizer and greedy WordPiece algorithm instead of a wrapper over `Microsoft.ML.Tokenizers`.
  Accents are stripped, all whitespace (including `\n`, `\t`, `\r`, `\v`, `\f`, NBSP, and line separators)
  separates words, control and zero width format characters are removed, ASCII and Unicode punctuation are
  split into their own tokens, and an unknown word becomes one `[UNK]`. Counts rise on accented, symbol
  heavy, and emoji text and change on multi line text. `Encode` returns identifiers without `[CLS]` and
  `[SEP]`, consistent with `CountTokens`, and `SliceByTokenRange` returns the exact original text covered by
  the token range.
- `SentenceBoundaryAware` and `SemanticBoundaryAware` overlap now choose, among the sentence or paragraph
  starts inside the chunk, the one whose overlap is closest to the requested amount, instead of snapping to
  the last boundary anywhere before the window.
- Hierarchy aware chunks now carry exact source offsets, including for CRLF input. A chunk whose text has a
  contextualized header prepended reports `-1`, since its text is not a source substring.
- `MergeForward` small chunk handling merges adjacent source spans when only whitespace separates them, which
  keeps the original separator and exact offsets. Pieces that cannot be merged that way are still joined
  with a newline and report `-1`.
- Chunking is substantially faster. On a 622 KB novel, fixed token chunking with overlap dropped from about
  21 s to under 1 s with cl100k (excluding the one time encoder load) and from about 31 s to under 1 s with
  WordPiece, and the sentence and recursive strategies run roughly two to four times faster.

### Added

- `ChunkingOptions.SafetyMarginTokens` and `ChunkingOptions.SafetyMarginPercentage` hold back part of the
  resolved model budget for runtimes whose tokenizer can count slightly more than the local one. The margin
  comes off the model's effective budget, not off a `MaxTokens` that is already smaller. Both default to 0.
- `WordPieceOptions` (`LowerCase`, `StripAccents`, `TokenizeCjkCharacters`, `MaxInputCharactersPerWord`,
  `UnknownToken`) and `BertWordPieceTokenizerAdapter` constructors that take options or a caller supplied
  `vocab.txt` stream, for example a cased BERT vocabulary. `MaxInputCharactersPerWord` defaults to 0 (no
  limit) because llama.cpp based runtimes segment long words fully instead of collapsing them to one
  `[UNK]` as Hugging Face does; the higher count is the safe one.
- The `textchunker.chunks_suppressed` counter on the `TextChunker` meter. A backstop drops any chunk whose
  source span lies inside the previous chunk's span and counts it here. The span based strategies never
  produce one, so a nonzero value signals a regression.
- A `--write-parity-golden <path>` option on `Test.Automated` that recomputes the chunk boundary fixture for
  review and re approval after an intended boundary change.

### Removed

- The internal decode and re encode slicing loop and its sentence and paragraph boundary adjusters, which
  worked in token positions that did not correspond to the text they sliced.

## [0.2.2] - 2026-09-23

### Added

- A `KnownModel` value on `TokenizationProfileSourceEnum`. An exact match in the known embedding model
  registry now resolves with `ProfileSource = KnownModel` and `UsedFallback = false`, so consumers can
  distinguish an authoritative known-model configuration from an API format provider default chosen by
  name heuristics (`ProviderDefault`, still `UsedFallback = true`).

### Fixed

- BERT family WordPiece profiles now reserve 2 input tokens for the `[CLS]` and `[SEP]` special tokens the
  embedding endpoint adds. The offline WordPiece tokenizer does not count these, so a chunk filled to a
  model's full sequence length would overflow by two tokens once the endpoint wrapped it. The effective
  chunking budget for a known BERT model is now its sequence length minus 2 (for example `all-minilm`
  yields 254 and `nomic-embed-text` yields 2046). Introduced the configurable
  `TokenizationDefaults.BertReservedInputTokens` (default 2), applied to the seeded known models, the
  generic BERT heuristic, and explicit `TokenizerKind` overrides.

## [0.2.1] - 2026-09-23

### Added

- A configurable known-model registry on `TokenizationDefaults`. During profile resolution a model
  identifier is normalized (a provider path prefix such as `sentence-transformers/` and a version or
  quantization tag such as `:latest` or `:v1.5` are stripped) and matched against the registry, longest
  key first, so a specific entry wins over a generic one. A match takes precedence over the API format,
  since the model identifier is the more authoritative signal. The table is seeded with common embedding
  models out of the box, including `nomic-embed-text` (2048), `all-MiniLM` (256), `all-mpnet-base-v2`
  (384), `bge`, `gte`, `e5`, and `mxbai-embed-large` (512), `bge-m3` (8192), and the OpenAI
  `text-embedding-3-*` and `text-embedding-ada-002` models (cl100k, 8191).
- `TokenizationDefaults.RegisterKnownModel` and the mutable `TokenizationDefaults.KnownModels` dictionary,
  so a consumer can add or override entries at startup without a code change.
- `TokenizationDefaultEntry.ReservedInputTokens` (with a matching constructor overload). The profile
  resolver now populates `ResolvedTokenizationProfile.ReservedInputTokens` from the resolved entry.

### Changed

- `TokenizationDefaults.IsBertLikeModel` now also recognizes `mpnet`, `nomic`, and `mxbai`, so those
  models route to the BERT WordPiece tokenizer instead of falling through to cl100k.

## [0.2.0] - 2026-09-22

### Changed

- **Breaking:** `Chunk.ParentGUID` is now `Guid?` (nullable) and defaults to `null`. It is a parent
  identifier (null when no parent was supplied), and the chunker no longer backfills it with the source
  content's own GUID. Set `ChunkingOptions.ParentGUID` or the request's `ParentGUID` to stamp a value.
- **Breaking:** renamed `AtomTypeEnum` to `ContentTypeEnum`. The enum describes the content type of an
  input (text, list, table, and so on) and drives strategy routing. The old name was inherited
  terminology that did not fit a chunking library. Update `ContentRequest.Type` and
  `ChunkingOptions.InputType` assignments from `AtomTypeEnum.X` to `ContentTypeEnum.X`.
- **Breaking:** renamed `SemanticCellRequest` to `ContentRequest`. It is the structured content input to
  `ChunkRequest`; "semantic cell" was inherited graph terminology. The members are unchanged; only the type
  name and its "cell" wording changed to "content".

### Removed

- **Breaking:** removed `BatchLimitModeEnum` and the `BatchLimitMode` property from
  `ResolvedTokenizationProfile` and `TokenizationCalibrationResult`. Batch limit mode described how an
  embedding endpoint applies token limits across batched inputs, which the chunker never used.

## [0.1.0] - 2026-09-21

The first release. A best-of-breed, tokenizer-agnostic chunking library for retrieval augmented generation
and embedding pipelines, with an async streaming API, exact token-boundary slicing, and rich chunk metadata.

### Added

- Async streaming API. `IChunker` exposes `ChunkText`, `ChunkStream`, and `ChunkFile` for unstructured
  content and `ChunkList`, `ChunkTable`, and `ChunkRequest` for structured content. Every entry point
  routes through one internal pipeline so behavior cannot drift between input modes.
- A `Chunk` result that carries identity, ordering, sizing, provenance, and integrity together: GUID,
  ParentGUID, position, token count, character count, source offsets, strategy, tokenizer model,
  header context, labels, tags, and optional MD5, SHA1, and SHA256 hashes.
- Twelve strategies: fixed token, recursive, sentence, paragraph, regex, whole list, list entry, and five
  table renderings, each degrading gracefully when a unit exceeds the budget.
- A recursive separator splitter with a configurable ladder and structure aware presets via `Format`
  (`Plain`, `Markdown`, `CSharp`, `Python`, `JavaScript`, `Java`).
- Overlap expressed three ways: token count, percentage of chunk size, or character count.
- A pluggable tokenizer stack behind `ITokenizerAdapter`, with cl100k and o200k (SharpToken), BERT
  WordPiece (Microsoft.ML.Tokenizers with an embedded vocabulary), and an `MlTokenizerAdapter` that wraps
  any Microsoft.ML.Tokenizers tokenizer for Hugging Face and SentencePiece models.
- A tokenization profile resolver with a four tier precedence (override, calibration, provider default,
  global fallback) and an `ITokenizerCalibrationProbe` hook for optional live calibration without an
  HTTP dependency in the core package.
- The strict slice guard that re encodes every token slice and shrinks it until it provably fits, so no
  chunk ever exceeds the budget after a decode round trip.
- Hierarchy aware chunking that builds a markdown header tree and stamps each chunk with a breadcrumb,
  with an option to prepend that breadcrumb into the chunk text for better embeddings.
- A retrieval-quality evaluation harness (`TextChunker.Evaluation`) that scores configurations by token
  span recall, precision, and IoU against golden answer spans, with no embedder required.
- Context prefix support with real budget accounting, small chunk merging or dropping, an input size
  guard, and a configurable regex timeout.
- Presets `ForRag`, `ForSummarization`, and `ForLargeContext`.
- A JSON serialization contract (`ChunkerJson`) with strict enum handling, an `ActivitySource` and a
  `Meter` for tracing and metrics, and an `AddTextChunker` dependency injection extension.
- A companion package `TextChunker.Extensions.DataIngestion` that implements `IngestionChunker<string>`,
  so TextChunker can serve as the chunking stage of a Microsoft.Extensions.DataIngestion pipeline. The
  core package takes no dependency on the preview DataIngestion abstractions.
- An interactive console driver (`TextChunkerConsole`), a four project Touchstone test suite with a
  golden boundary fixture, and a BenchmarkDotNet harness.

### Fixed

- Request `Labels` and `Tags` are now echoed onto every produced chunk. The `Chunk` model always exposed
  them, but the enrichment path did not populate them from the request.
- The token budget guarantee now holds on the emitted chunk text. Trimming a leading space could previously
  raise a chunk's token count above the budget under byte-pair tokenizers, because a leading-space word is
  often a single token that splits without the space. Whitespace is now trimmed only when doing so does not
  increase the token count.

### Notes

- Multi targets net8.0, net10.0, and netstandard2.0 and ships a symbol package. The netstandard2.0 target
  extends reach to .NET Framework 4.6.1 and later and other netstandard2.0 runtimes.
- Docker, REST, MCP, and SDK repository assets do not apply to this package. The NuGet package is the
  distributable, so no `DOCKERHUB_README.md`, `.dockerignore`, `REST_API.md`, `MCP_API.md`, Postman
  collection, or `sdk/` tree is present.
