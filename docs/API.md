# API Reference

This document describes the public surface of TextChunker. The primary namespace is `TextChunker.Chunking`
for the chunker and `TextChunker.Models` for the data types.

## IChunker

The entry point interface, implemented by `Chunker`. Every method returns `IAsyncEnumerable<Chunk>` and
accepts a trailing `CancellationToken`.

| Method | Purpose |
|---|---|
| `ChunkText(string, ChunkingOptions?, CancellationToken)` | Chunk a text string. |
| `ChunkStream(Stream, ChunkingOptions?, Encoding?, CancellationToken)` | Chunk a stream. The stream is read but not disposed. |
| `ChunkFile(string, ChunkingOptions?, FileChunkingOptions?, CancellationToken)` | Chunk a file by name, with an access mode. |
| `ChunkList(IEnumerable<string>, bool ordered, ChunkingOptions?, CancellationToken)` | Chunk a list. |
| `ChunkTable(IReadOnlyList<IReadOnlyList<string>>, ChunkingOptions?, CancellationToken)` | Chunk a table. Row zero is the header. |
| `ChunkRequest(ContentRequest, ChunkingOptions?, CancellationToken)` | Chunk a structured request, including a hierarchy of child content. |

Passing `null` options uses a default `ChunkingOptions`.

## Chunker

`Chunker` implements `IChunker`. Constructors:

- `Chunker()` resolves the tokenizer from options.
- `Chunker(ITokenizerAdapter tokenizer)` always uses the supplied tokenizer.
- `Chunker(ITokenizerAdapter?, ITokenizerCalibrationProbe?)` adds a calibration probe.

Beyond the interface, `Chunker` adds convenience methods:

- `IReadOnlyList<Chunk> Chunk(string, ChunkingOptions?)` drains the stream synchronously.
- `Task<ChunkingResult> ChunkToResultAsync(string, ChunkingOptions?, CancellationToken)` returns chunks
  plus run level diagnostics.

A single instance holds no per call mutable state and is safe to share across threads.

## Chunk

The produced chunk. See the README for the full field list. Notable points: `Text` is never null,
`StartOffset` and `EndOffset` are `-1` when the chunk is serialized list or table output rather than a
literal substring, and `HeaderContext` is null unless hierarchy aware chunking was requested.

## ChunkingOptions

The parameters that control an operation. Properties with a range or nullability constraint validate on
assignment and throw `InvalidChunkingOptionsException`. See [STRATEGIES.md](STRATEGIES.md) for how the
overlap options interact and [TOKENIZERS.md](TOKENIZERS.md) for the tokenizer options. Presets:
`ChunkingOptions.ForRag()`, `ForSummarization()`, and `ForLargeContext()`.

## Tokenization

`ITokenizerAdapter` exposes `CountTokens`, `Encode`, `Decode`, and `SliceByTokenRange`. Two adapters
ship: `SharpTokenTokenizerAdapter` and `BertWordPieceTokenizerAdapter`. `TokenizerAdapterFactory.Create`
builds one from a `ResolvedTokenizationProfile`. `TokenizationProfileResolver` resolves a profile, and
`ITokenizerCalibrationProbe` is the optional live calibration hook.

## Serialization

`ChunkerJson.Serialize` and `Deserialize` use a shared `System.Text.Json` contract with camelCase
properties and strict enum handling. An unknown enum string is rejected on read rather than binding to the
zero value.

## Dependency injection

`ServiceCollectionExtensions.AddTextChunker` registers `IChunker`. Overloads accept a calibration probe or
an explicit tokenizer.

## Exceptions

- `ChunkingException` for chunking failures.
- `TokenizerResolutionException` for tokenizer or profile resolution failures.
- `InvalidChunkingOptionsException` for invalid options and oversize input.
