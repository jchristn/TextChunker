# CLAUDE.md

Guidance for working in this repository.

## What this is

TextChunker is a NuGet library that chunks text and structured content for RAG and embedding pipelines.
The core library multi targets `net8.0`, `net10.0`, and `netstandard2.0`; the other projects target
`net8.0;net10.0`. On netstandard2.0 the core adds polyfill packages (System.Text.Json,
System.Diagnostics.DiagnosticSource, Microsoft.Bcl.AsyncInterfaces) for types that are in-box on net8 and
net10, and pins Microsoft.Bcl.Memory to a patched version. The NuGet package is the only distributable.
There is no server, Docker image, dashboard, REST or MCP surface, or SDK tree, so those repository
requirements do not apply.

## Layout

```
src/
  TextChunker.sln
  Directory.Build.props        shared TFMs, nullable, version, authorship
  TextChunker/                 the library
    Chunkers/                  the seven strategy chunkers (internal)
    Chunking/                  the sizing core, dispatcher, and public Chunker
    Enums/                     one enum per file
    Exceptions/                domain exception types
    Models/                    public models (Chunk, ChunkingOptions, and so on)
    Tokenization/              adapters, factory, resolver, embedded vocab
    Serialization/             JSON contract and strict enum converter
    DependencyInjection/       AddTextChunker
    Observability/             ActivitySource and Meter
  TextChunker.Extensions.DataIngestion/  IngestionChunker<string> adapter (preview dependency)
  Test.Extensions/             xUnit tests for the DataIngestion adapter
  TextChunkerConsole/          interactive driver
  Test.Shared/                 Touchstone suite descriptors (no console output)
  Test.Automated/              console runner
  Test.Xunit/                  xUnit fact and theory runners
  Test.Nunit/                  NUnit fact and case runners
  TextChunker.Benchmarks/      BenchmarkDotNet harness (not in the solution)
  TextChunker.Evaluation/      retrieval-quality harness (not in the solution)
```

## Build and test

```
dotnet build src/TextChunker.sln
dotnet run --project src/Test.Automated                 # console runner, exit 0 on pass
dotnet run --project src/Test.Automated -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
dotnet pack src/TextChunker/TextChunker.csproj -c Release
dotnet run -c Release --project src/TextChunker.Benchmarks -- --filter *   # benchmarks, run deliberately
dotnet run -c Release --project src/TextChunker.Evaluation                 # retrieval-quality scores
```

## Code style

These rules are enforced by convention and `.editorconfig`. Follow them strictly.

- Namespace declaration at the top, using statements inside the namespace block.
- System and Microsoft usings first in alphabetical order, then other usings in alphabetical order.
- Public members, constructors, and public methods carry XML documentation. Private members do not.
- Use explicit types, never `var`.
- No tuples.
- Private fields are `_PascalCase` (underscore then Pascal case).
- Validated or nullable public members use explicit getters and setters over backing fields.
- Async methods accept a `CancellationToken` and use `.ConfigureAwait(false)`.
- Guard clauses at method entry. Specific exception types with contextual messages. Document throws with
  `<exception>` tags.
- One class or one enum per file.
- No `Console.WriteLine` in library code. The console project is the only place console output belongs.
- Never use em dashes anywhere.

## Versioning

Do not change the version number without an explicit request. The version lives in
`src/Directory.Build.props` and is `0.1.0`.

## Testing model

Test logic lives once in `Test.Shared` as Touchstone `TestCaseDescriptor` instances and runs through the
console, xUnit, and NUnit front ends. `Test.Shared` references only `Touchstone.Core` and the library and
must never write to the console. The golden fixture `Test.Shared/Fixtures/chunking-parity.json` pins chunk
boundaries; a change that shifts a boundary shows up as a fixture failure that must be explained and the
fixture re approved.
