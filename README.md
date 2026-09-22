# TextChunker

TextChunker turns text, streams, files, lists, and tables into clean, token sized chunks for retrieval
augmented generation and embedding pipelines. It counts tokens with real tokenizers rather than
character heuristics, guarantees no chunk ever exceeds your budget, and hands back a chunk that already
knows its parent, its ordinal, its token count, and where it came from in the source.

It targets `net8.0`, `net10.0`, and `netstandard2.0` and ships as a single NuGet package with a symbol
package. The `netstandard2.0` target extends reach to .NET Framework 4.6.1+, older Unity runtimes, and
Xamarin.

## Why it exists

Most chunkers make you choose between correct token counting and a clean API. TextChunker gives you
both. The token budget is enforced by re encoding every slice and shrinking it until it provably fits,
so a 512 token limit means 512 tokens, not an estimate that overflows the moment you send it to a model.
The tokenizer is pluggable: cl100k for OpenAI style models and BERT WordPiece for MiniLM, BGE, and E5,
with the vocabulary embedded in the assembly so counts are correct offline with no external file.

The result object was designed to survive the trip into a vector store. Each chunk carries a parent
identifier, a zero based ordinal, its token and character counts, and, for text strategies, the exact
character offsets back into the source. You can group by parent, order by position, re embed, and cite a
passage without building a second data structure alongside the chunks.

## Install

```
dotnet add package TextChunker
```

## Quick start

The API is asynchronous and streams chunks as they are produced. Passing `null` options uses sensible
defaults.

```csharp
using TextChunker.Chunking;
using TextChunker.Models;
using TextChunker.Enums;

IChunker chunker = new Chunker();

await foreach (Chunk chunk in chunker.ChunkText("Your document text goes here.",
    new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 512, OverlapCount = 64 }))
{
    Console.WriteLine($"[{chunk.Position}] {chunk.TokenCount} tokens: {chunk.Text}");
}
```

Three entry points cover unstructured input, and the file overload carries its access mode so you
control sharing and encoding.

```csharp
// From a stream (the stream is read but not disposed)
await foreach (Chunk c in chunker.ChunkStream(stream, options)) { }

// From a file, with an access mode
await foreach (Chunk c in chunker.ChunkFile("notes.md", options,
    new FileChunkingOptions { AccessMode = FileAccessModeEnum.SharedRead })) { }
```

Structured content that carries shape uses its own entry points, because a flat string cannot express a
table's rows or a list's items.

```csharp
// A list, driving the ListEntry or WholeList strategy
await foreach (Chunk c in chunker.ChunkList(new[] { "first", "second", "third" }, ordered: false, options)) { }

// A table, where row zero is the header
var rows = new List<IReadOnlyList<string>>
{
    new[] { "Name", "Role" },
    new[] { "Ada", "Engineer" }
};
await foreach (Chunk c in chunker.ChunkTable(rows,
    new ChunkingOptions { Strategy = ChunkStrategyEnum.RowWithHeaders })) { }
```

When you want everything at once instead of a stream, `ChunkToResultAsync` returns the chunks plus run
level diagnostics, and the synchronous `Chunk(text, options)` convenience method drains the stream to a
list.

## Strategies

| Strategy | Input | Behavior |
|---|---|---|
| `FixedTokenCount` | text | Sliding token window with optional overlap |
| `Recursive` | text | Recursively splits on a separator ladder, then merges to fill the budget |
| `SentenceBased` | text | Groups whole sentences up to the budget |
| `ParagraphBased` | text | Groups paragraphs, falling back to sentences when one is too large |
| `RegexBased` | text | Splits on your pattern, guarded by a timeout |
| `WholeList` | list | The whole list as one chunk, split by line if it overflows |
| `ListEntry` | list | One chunk per item |
| `Row` | table | Each data row as space joined values |
| `RowWithHeaders` | table | Each data row as a small markdown table |
| `RowGroupWithHeaders` | table | Groups of rows as a markdown table |
| `KeyValuePairs` | table | Each row as `header: value` pairs |
| `WholeTable` | table | The whole table as one markdown table |

Every strategy degrades gracefully. A paragraph larger than the budget becomes sentences, an oversized
sentence becomes token spans, and an oversized table group becomes rows, then cells, then token spans.
Cell values that contain a pipe are escaped so serialized markdown stays intact.

The `Recursive` strategy is the one to reach for on structured text. It walks a ladder of separators
(paragraph, line, sentence, word by default) and only descends to a finer split when a piece still
overflows, then merges neighbors back up to the budget. Set `Format` to `Markdown`, `CSharp`, `Python`,
`JavaScript`, or `Java` to prefer headings or code declarations as split points, or supply your own
`Separators` list.

Overlap can be expressed three ways. `OverlapCount` is a token count, `OverlapPercentage` is a fraction
of the chunk size, and `OverlapCharacters` is a character count converted to an approximate token
overlap. Percentage wins over characters, which wins over count when more than one is set.

## Tokenizers and budgets

The chunker resolves a tokenizer and a token budget once per call. By default it uses cl100k. Set a
`ModelId` or an `ApiFormat` and it picks the right family and budget: OpenAI and vLLM resolve to cl100k
at 8192, GPT-4o and o-series names resolve to o200k, Gemini to cl100k at 2048, and BERT family names such
as `all-minilm`, `bge-small`, or `e5-base` to WordPiece at 512.

For a tokenizer the resolver does not know, wrap any `Microsoft.ML.Tokenizers` tokenizer (a Hugging Face
JSON tokenizer, or a SentencePiece model for Llama or Gemma) in `MlTokenizerAdapter` and hand it to the
`Chunker` constructor, so token counts match your exact model.

```csharp
ChunkingOptions options = new ChunkingOptions
{
    Strategy = ChunkStrategyEnum.SentenceBased,
    MaxTokens = 256,
    ModelId = "all-minilm"   // resolves to BERT WordPiece, 512 token model budget
};
```

The actual budget enforced is the smaller of your `MaxTokens` and the model's limit, so a chunk never
exceeds either. If you host models behind an endpoint that reports a different limit, implement
`ITokenizerCalibrationProbe` and pass it to the chunker to discover the true budget at runtime. The core
package opens no network connections on its own.

## The chunk

```csharp
public class Chunk
{
    public Guid GUID;              // unique per chunk
    public Guid ParentGUID;        // groups chunks from the same source
    public int Position;           // zero based ordinal within the parent
    public string Text;
    public int TokenCount;
    public int CharacterCount;
    public int StartOffset;        // source offset, or -1 for serialized list/table output
    public int EndOffset;
    public ChunkStrategyEnum Strategy;
    public string TokenizerModel;
    public string? HeaderContext;  // breadcrumb when hierarchy aware
    public List<string> Labels;
    public Dictionary<string, string> Tags;
    public List<float> Embeddings; // reserved for a downstream embedder
    public byte[]? MD5Hash, SHA1Hash, SHA256Hash;
}
```

Offsets, hashes, and token counts are each gated by an option, so you pay only for what you use.

## Hierarchy aware chunking

Turn on `HierarchyAware` and TextChunker parses a markdown header tree, chunks each section, and stamps
every chunk with a breadcrumb such as `Guide > Setup > Windows` in `HeaderContext`. Set
`ContextualizeHeaders` to prepend that breadcrumb into the chunk text itself for better embeddings; its
token cost is charged against the budget rather than silently inflating the chunk.

## Dependency injection

```csharp
using TextChunker.DependencyInjection;

services.AddTextChunker();
```

## Microsoft.Extensions.DataIngestion

The companion package `TextChunker.Extensions.DataIngestion` lets TextChunker serve as the chunking stage
of a `Microsoft.Extensions.DataIngestion` pipeline. It implements `IngestionChunker<string>`, so it slots
in next to Microsoft's readers, enrichers, and vector-store writers, and it carries TextChunker's metadata
(position, token count, offsets, strategy, header context) onto each `IngestionChunk`.

```csharp
using TextChunker.Extensions.DataIngestion;

IngestionChunker<string> chunker = new TextChunkerIngestionChunker(
    new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, Format = ContentFormatEnum.Markdown, HierarchyAware = true });

await foreach (IngestionChunk<string> chunk in chunker.ProcessAsync(document))
{
    // chunk.Content, chunk.Context (breadcrumb), chunk.Document, chunk.Metadata["textchunker.*"]
}
```

The package depends on the preview `Microsoft.Extensions.DataIngestion.Abstractions`, so it is kept
separate from the core package, which takes no such dependency.

## Observability

The library never writes to the console. It exposes an `ActivitySource` and a `Meter`, both named
`TextChunker`, so you can trace chunking and watch chunk size distributions with your existing telemetry.

## Building and testing

```
dotnet build src/TextChunker.sln
dotnet run --project src/Test.Automated
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

The console driver lets you feel the library by hand:

```
dotnet run --project src/TextChunkerConsole
```

## Documentation

- [docs/API.md](docs/API.md), the public API surface.
- [docs/STRATEGIES.md](docs/STRATEGIES.md), each strategy with worked examples.
- [docs/TOKENIZERS.md](docs/TOKENIZERS.md), tokenizer families and budget resolution.
- [docs/COOKBOOK.md](docs/COOKBOOK.md), task oriented recipes.
- [docs/EVALUATION.md](docs/EVALUATION.md), the retrieval-quality evaluation harness.

## License

MIT. See [LICENSE.md](LICENSE.md).
