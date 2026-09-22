# Strategies

Each strategy is selected with `ChunkingOptions.Strategy`. Text strategies apply to text, stream, file,
and text typed requests. List and table strategies apply to the structured entry points.

## Text strategies

`FixedTokenCount` slides a token window across the text. It is the most predictable strategy and the right
default when you want uniform chunk sizes. Overlap is measured in tokens.

`Recursive` walks a ladder of separators from coarse to fine, descending only when a piece still exceeds
the budget, then merges neighbors back up to the budget. This is the structure aware approach most modern
chunkers use, and it usually gives better boundaries than a fixed window because it prefers to break at
paragraphs, then lines, then sentences, then words. The ladder comes from `Format` (`Plain`, `Markdown`,
`CSharp`, `Python`, `JavaScript`, `Java`) unless you set `Separators` explicitly, which overrides `Format`.
For markdown, the ladder prefers heading and fenced code boundaries; for a language, it prefers type and
member declarations.

`SentenceBased` splits on sentence boundaries and packs whole sentences up to the budget. Overlap is
measured in whole sentences. A single sentence larger than the budget falls back to token spans.

`ParagraphBased` splits on blank lines and packs paragraphs. A paragraph larger than the budget falls back
to sentence chunking, which in turn falls back to token spans. This is a good fit for prose where you want
chunks to respect paragraph structure.

`RegexBased` splits on a user supplied pattern in `RegexPattern`. The regex runs compiled and multiline,
guarded by `RegexTimeoutMilliseconds` (default 5000) against catastrophic backtracking. A segment larger
than the budget falls back to token spans. `RegexPattern` is required; omitting it throws.

## List strategies

`WholeList` serializes the entire list into one chunk, numbered or bulleted, and splits by line only when
the whole list exceeds the budget. `ListEntry` produces one chunk per item, with a token span fallback for
an oversized item.

## Table strategies

Table strategies assume row zero is the header. `Row` emits each data row as space joined values.
`RowWithHeaders` emits each data row as a small markdown table. `RowGroupWithHeaders` groups rows using
`RowGroupSize`. `KeyValuePairs` emits each row as `header: value` pairs. `WholeTable` emits the whole
table, degrading to row groups when it overflows. Cell values containing a pipe are escaped so the
markdown stays valid.

## Overlap

Overlap has one model across strategies. Token strategies overlap by tokens; unit strategies (sentence,
paragraph, recursive) overlap by whole units. Three properties express the amount: `OverlapCount` is a
token count, `OverlapPercentage` is a fraction of the chunk size, and `OverlapCharacters` is a character
count converted to an approximate token overlap using the text's average token density. When more than one
is set, percentage wins over characters, which wins over count. There is no hidden multiplier.

`OverlapStrategy` controls boundary handling when overlap is active. `SlidingWindow` is mechanical.
`SentenceBoundaryAware` snaps the next start back to the last sentence boundary. `SemanticBoundaryAware`
snaps to the last paragraph boundary. The advance always makes net forward progress, so the loop
terminates even when overlap is set at or above the chunk size.

## Small chunks

`MinChunkTokens` with `SmallChunkMode` controls what happens to chunks below a threshold. `Keep` is the
default. `MergeForward` merges a small chunk into the next one where the combined chunk still fits.
`Drop` removes small chunks. This is useful for trimming short trailing fragments.
