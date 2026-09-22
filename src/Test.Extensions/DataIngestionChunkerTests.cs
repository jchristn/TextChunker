namespace Test.Extensions
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::Microsoft.Extensions.DataIngestion;
    using TextChunker.Enums;
    using TextChunker.Extensions.DataIngestion;
    using TextChunker.Models;
    using Xunit;

    /// <summary>
    /// Verifies the Microsoft.Extensions.DataIngestion adapter chunks an ingestion document, preserves the
    /// document reference, and attaches TextChunker metadata.
    /// </summary>
    public sealed class DataIngestionChunkerTests
    {
        private static IngestionDocument BuildDocument()
        {
            IngestionDocument document = new IngestionDocument("doc-1");

            IngestionDocumentSection section = new IngestionDocumentSection();
            section.Elements.Add(new IngestionDocumentHeader("Overview") { Level = 1 });
            section.Elements.Add(new IngestionDocumentParagraph(
                "TextChunker turns text into token sized chunks. It counts tokens with real tokenizers rather than "
                + "character heuristics. Every produced chunk fits within the configured token budget."));
            section.Elements.Add(new IngestionDocumentHeader("Details") { Level = 2 });
            section.Elements.Add(new IngestionDocumentParagraph(
                "The strict slice guard re encodes each slice until it fits. Offsets index back to the source text. "
                + "Metadata such as position and token count travels with every chunk."));
            document.Sections.Add(section);

            return document;
        }

        private static async Task<List<IngestionChunk<string>>> CollectAsync(IngestionChunker<string> chunker, IngestionDocument document)
        {
            List<IngestionChunk<string>> chunks = new List<IngestionChunk<string>>();
            await foreach (IngestionChunk<string> chunk in chunker.ProcessAsync(document))
                chunks.Add(chunk);
            return chunks;
        }

        [Fact]
        public async Task ProducesChunksWithMetadataAndDocument()
        {
            TextChunkerIngestionChunker chunker = new TextChunkerIngestionChunker(
                new ChunkingOptions { Strategy = ChunkStrategyEnum.Recursive, MaxTokens = 40 });

            List<IngestionChunk<string>> chunks = await CollectAsync(chunker, BuildDocument());

            Assert.True(chunks.Count > 1);
            for (int i = 0; i < chunks.Count; i++)
            {
                IngestionChunk<string> chunk = chunks[i];
                Assert.False(string.IsNullOrWhiteSpace(chunk.Content));
                Assert.Equal("doc-1", chunk.Document.Identifier);
                Assert.True(chunk.HasMetadata);
                Assert.Equal(i, chunk.Metadata["textchunker.position"]);
                Assert.True((int)chunk.Metadata["textchunker.tokenCount"!] <= 40);
            }
        }

        [Fact]
        public async Task ContextualizeHeadersPrependsBreadcrumb()
        {
            TextChunkerIngestionChunker chunker = new TextChunkerIngestionChunker(
                new ChunkingOptions
                {
                    Strategy = ChunkStrategyEnum.Recursive,
                    Format = ContentFormatEnum.Markdown,
                    HierarchyAware = true,
                    ContextualizeHeaders = true,
                    MaxTokens = 40
                });

            List<IngestionChunk<string>> chunks = await CollectAsync(chunker, BuildDocument());

            bool foundNested = false;
            foreach (IngestionChunk<string> chunk in chunks)
            {
                if (chunk.Context == "Overview > Details")
                    foundNested = true;
            }
            Assert.True(foundNested);
        }

        [Fact]
        public async Task EmptyDocumentProducesNoChunks()
        {
            TextChunkerIngestionChunker chunker = new TextChunkerIngestionChunker();
            IngestionDocument document = new IngestionDocument("empty");

            List<IngestionChunk<string>> chunks = await CollectAsync(chunker, document);

            Assert.Empty(chunks);
        }

        [Fact]
        public async Task TableAndImageAreChunked()
        {
            IngestionDocument document = new IngestionDocument("doc-2");
            IngestionDocumentSection section = new IngestionDocumentSection();
            section.Elements.Add(new IngestionDocumentHeader("Report") { Level = 1 });

            IngestionDocumentElement?[,] cells = new IngestionDocumentElement?[2, 2];
            cells[0, 0] = new IngestionDocumentParagraph("Name");
            cells[0, 1] = new IngestionDocumentParagraph("Note");
            cells[1, 0] = new IngestionDocumentParagraph("Ada");
            cells[1, 1] = new IngestionDocumentParagraph("a|b|c");
            section.Elements.Add(new IngestionDocumentTable("| Name | Note |", cells));

            section.Elements.Add(new IngestionDocumentImage("![](chart.png)") { AlternativeText = "quarterly revenue chart" });
            document.Sections.Add(section);

            TextChunkerIngestionChunker chunker = new TextChunkerIngestionChunker(
                new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 256 });

            List<IngestionChunk<string>> chunks = await CollectAsync(chunker, document);

            Assert.NotEmpty(chunks);
            string all = string.Join("\n", chunks.ConvertAll(c => c.Content));
            Assert.Contains("Ada", all);
            Assert.Contains("Note", all);
            Assert.Contains("a\\|b\\|c", all);
            Assert.Contains("quarterly revenue chart", all);
            Assert.True(chunks[0].HasMetadata);
            Assert.True(chunks[0].Metadata.ContainsKey("textchunker.tokenCount"));
        }

        [Fact]
        public async Task NestedSectionsFooterAndEmptyCell()
        {
            IngestionDocument document = new IngestionDocument("doc-3");

            IngestionDocumentSection top = new IngestionDocumentSection();
            top.Elements.Add(new IngestionDocumentHeader("Top") { Level = 1 });
            top.Elements.Add(new IngestionDocumentParagraph("Top level content that introduces the document body."));

            IngestionDocumentSection nested = new IngestionDocumentSection();
            nested.Elements.Add(new IngestionDocumentHeader("Nested") { Level = 2 });
            nested.Elements.Add(new IngestionDocumentParagraph("Nested content that lives inside the top section."));
            top.Elements.Add(nested);

            IngestionDocumentElement?[,] cells = new IngestionDocumentElement?[2, 2];
            cells[0, 0] = new IngestionDocumentParagraph("Key");
            cells[0, 1] = new IngestionDocumentParagraph("Value");
            cells[1, 0] = new IngestionDocumentParagraph("Ready");
            cells[1, 1] = null;
            top.Elements.Add(new IngestionDocumentTable("| Key | Value |", cells));

            top.Elements.Add(new IngestionDocumentFooter("page 1 of 1"));
            document.Sections.Add(top);

            TextChunkerIngestionChunker chunker = new TextChunkerIngestionChunker(
                new ChunkingOptions { Strategy = ChunkStrategyEnum.FixedTokenCount, MaxTokens = 256 });

            List<IngestionChunk<string>> chunks = await CollectAsync(chunker, document);

            Assert.NotEmpty(chunks);
            string all = string.Join("\n", chunks.ConvertAll(c => c.Content));
            Assert.Contains("Top level content", all);
            Assert.Contains("Nested content", all);
            Assert.Contains("Ready", all);
        }
    }
}
