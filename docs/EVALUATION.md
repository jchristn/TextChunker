# Evaluation

Chunk boundaries are only good if they help retrieval. The `TextChunker.Evaluation` project measures that
directly, so defaults and changes can be judged with numbers instead of intuition.

## What it measures

The harness follows an established token-span overlap method for judging chunk boundaries, adapted to need
no embedding model. Each
document in the corpus declares golden answer spans, the character ranges a query's answer occupies. For a
given chunking configuration, the harness chunks the document and, for each span, selects the chunks that
actually overlap it (an oracle retrieval, which holds retrieval quality fixed and isolates the chunker).
It then scores three token span metrics:

- Recall: how much of the answer span the covering chunks contain. Low recall means the answer was split
  across a gap.
- Precision: how little surrounding noise those chunks add. Low precision means each chunk drags in a lot
  of unrelated text.
- IoU: intersection over union, which balances the two.

Because it uses the character offsets TextChunker already produces for text strategies, the harness needs
no embedder and runs in a second.

## Running it

```
dotnet run -c Release --project src/TextChunker.Evaluation
```

It prints a table of configurations sorted by IoU, with mean recall, precision, IoU, chunk count, and
chunk size. Use it to compare strategies and sizes on your own corpus by editing `EvaluationCorpus` to add
documents and answer spans.

## Reading the results

Recall near 1.0 across configurations is expected when chunks tile the document, because the answer is
always covered by some chunk. Precision and IoU are where configurations separate: smaller chunks bound an
answer more tightly and score higher, up to the point where a chunk becomes smaller than the answer and
recall starts to fall. The right size is the one that maximizes IoU for your content, and this harness is
how you find it rather than guess it.
