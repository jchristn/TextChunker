# Cookbook

Task oriented recipes for common jobs.

## Chunk for a RAG index

Small overlapping token chunks work well for retrieval. The `ForRag` preset sets a 512 token size with 64
tokens of overlap.

```csharp
IChunker chunker = new Chunker();
await foreach (Chunk c in chunker.ChunkText(document, ChunkingOptions.ForRag()))
{
    await index.UpsertAsync(c.ParentGUID, c.Position, c.Text, c.GUID);
}
```

Group retrieved hits back to their source with `ParentGUID` and order them with `Position`.

## Match a specific embedding model

Set `ModelId` so the token budget matches the model you will embed with. For an all-MiniLM model the
budget resolves to 512 WordPiece tokens automatically.

```csharp
ChunkingOptions options = new ChunkingOptions
{
    Strategy = ChunkStrategyEnum.SentenceBased,
    MaxTokens = 512,
    ModelId = "all-minilm"
};
```

## Preserve document structure

For a markdown document, turn on hierarchy awareness so each chunk knows which section it came from.

```csharp
ChunkingOptions options = new ChunkingOptions { HierarchyAware = true, MaxTokens = 384 };
await foreach (Chunk c in chunker.ChunkText(markdown, options))
{
    string breadcrumb = c.HeaderContext ?? "(root)";
    // store breadcrumb alongside the chunk for better retrieval context
}
```

## Chunk a table for retrieval

`KeyValuePairs` produces one self describing chunk per row, which retrieves well because each chunk names
its own columns.

```csharp
await foreach (Chunk c in chunker.ChunkTable(rows,
    new ChunkingOptions { Strategy = ChunkStrategyEnum.KeyValuePairs, MaxTokens = 256 }))
{
    // "Name: Ada, Role: Engineer, Location: London"
}
```

## Cite a passage back to the source

For text strategies, `StartOffset` and `EndOffset` index back to the exact substring, so you can highlight
the passage in the original document.

```csharp
await foreach (Chunk c in chunker.ChunkText(source, options))
{
    if (c.StartOffset >= 0)
    {
        string original = source.Substring(c.StartOffset, c.EndOffset - c.StartOffset);
        // original equals c.Text for text strategies with no context prefix
    }
}
```

## Add a shared prefix for embedding

Some embedding models benefit from an instruction prefix. `ContextPrefix` is applied to each chunk and its
cost is charged against the budget, so the chunk still fits.

```csharp
ChunkingOptions options = new ChunkingOptions
{
    MaxTokens = 512,
    ContextPrefix = "query: "
};
```
