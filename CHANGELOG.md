# Changelog

All notable changes to TextChunker are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is below 1.0.0, any
release may carry breaking changes.

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
