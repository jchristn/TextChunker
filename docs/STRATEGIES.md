# Strategies

Each strategy is selected with `ChunkingOptions.Strategy`. Text strategies apply to text, stream, file,
and text typed requests. List and table strategies apply to the structured entry points.

Every text strategy works on spans of the original text, so every chunk it produces is an exact substring
of the source, carries resolved `StartOffset` and `EndOffset` values, and keeps the source's own
whitespace between the pieces it groups. Consecutive chunks have strictly increasing starts and ends, and
no chunk is ever contained in the one before it.

## Text strategies

`FixedTokenCount` slides a token window across the text. It is the most predictable strategy and the right
default when you want uniform chunk sizes. Windows start and end on word boundaries (each CJK ideograph
counts as a word), and each window holds as many whole words as fit the budget, verified against the real
token count. A single word larger than the budget, such as a long URL or an encoded blob, is split at
grapheme cluster boundaries, so a cut never separates a surrogate pair, an emoji ZWJ sequence, or a base
character from its combining marks. This window is also the fallback every other strategy uses for a unit
that is too large on its own.

`Recursive` walks a ladder of separators from coarse to fine, descending only when a piece still exceeds
the budget, then merges neighbors back up to the budget. This is the structure aware approach most modern
chunkers use, and it usually gives better boundaries than a fixed window because it prefers to break at
paragraphs, then lines, then sentences, then words. The ladder comes from `Format` (`Plain`, `Markdown`,
`CSharp`, `Python`, `JavaScript`, `Java`) unless you set `Separators` explicitly, which overrides `Format`.
For markdown, the ladder prefers heading and fenced code boundaries; for a language, it prefers type and
member declarations. Only a separator's whitespace is discarded at a chunk boundary: a separator that opens
a block after a line break (`\n## `, `\nclass `) keeps its visible part with the chunk that follows, and any
other separator (`. `) keeps it with the chunk that precedes, so a boundary never drops a heading marker, a
keyword, or sentence punctuation.

`SentenceBased` splits on sentence boundaries and packs whole sentences up to the budget. Overlap is
measured in whole sentences. A single sentence larger than the budget falls back to the token window.

When a strategy has to split something that is only slightly over the budget (an oversized paragraph, page,
sentence, or word) it does not leave a tiny remainder behind. If the last chunk of the split would hold under
a quarter of the budget, it is re split with the chunk before it at the most even unit boundary, so a unit of
260 tokens under a 254 token budget becomes two chunks of about 130 rather than 254 and 6. `FixedTokenCount`
keeps uniform windows and does not do this, and neither does packing with overlap.

`ParagraphBased` splits on blank lines and packs paragraphs. A paragraph larger than the budget falls back
to sentence chunking, which in turn falls back to the token window. This is a good fit for prose where you
want chunks to respect paragraph structure.

`RegexBased` splits on a user supplied pattern in `RegexPattern`. The regex runs compiled and multiline,
guarded by `RegexTimeoutMilliseconds` (default 5000) against catastrophic backtracking. The delimiter text
itself is not part of any chunk, except for captured groups, which are kept as segments as with
`Regex.Split`. A segment larger than the budget falls back to the token window. `RegexPattern` is
required; omitting it throws.

## List strategies

`WholeList` serializes the entire list into one chunk, numbered or bulleted, and splits by line only when
the whole list exceeds the budget. `ListEntry` produces one chunk per item, with a token window fallback
for an oversized item. Applied to plain text instead of a list, `ListEntry` packs lines and `WholeList`
keeps the text whole when it fits.

## Table strategies

Table strategies assume row zero is the header. `Row` emits each data row as space joined values.
`RowWithHeaders` emits each data row as a small markdown table. `RowGroupWithHeaders` groups rows using
`RowGroupSize`. `KeyValuePairs` emits each row as `header: value` pairs. `WholeTable` emits the whole
table, degrading to row groups when it overflows. Cell values containing a pipe are escaped so the
markdown stays valid. Serialized list and table chunks are not substrings of any source, so their offsets
are `-1`.

## Overlap

Overlap has one model across strategies. Token strategies overlap by tokens; unit strategies (sentence,
paragraph, recursive) overlap by whole units. Three properties express the amount: `OverlapCount` is a
token count, `OverlapPercentage` is a fraction of the chunk size, and `OverlapCharacters` is a character
count converted to an approximate token overlap using the text's average token density. When more than one
is set, percentage wins over characters, which wins over count. There is no hidden multiplier.

For the token window the guarantee is precise: consecutive chunks share the longest run of whole words
whose token count does not exceed the requested overlap, so the shared region is at most `OverlapCount`
tokens and within one word of it. A word is never split to hit the number exactly. If a word is larger than
the whole overlap, the shared region is empty at that boundary.

`OverlapStrategy` controls where the next chunk starts when overlap is active. `SlidingWindow` uses the
word based overlap above. `SentenceBoundaryAware` considers every sentence start inside the chunk and picks
the one whose overlap is closest to the requested amount, preferring more context on a tie, so each
following chunk starts at the beginning of a sentence. `SemanticBoundaryAware` does the same with paragraph
starts (a blank line). When a chunk holds no boundary of the requested kind, the sliding window start is
used. Each chunk always ends past the previous one, so the loop terminates and never repeats content even
when overlap is set at or above the chunk size; in that case the start advances by at least one word.

## Small chunks

`MinChunkTokens` with `SmallChunkMode` controls what happens to chunks below a threshold. `Keep` is the
default. `MergeForward` merges a small chunk into the next one where the combined chunk still fits; when
only whitespace separates the two in the source, the merged chunk is the source span covering both, which
keeps the original separator and exact offsets. `Drop` removes small chunks. This is useful for trimming
short trailing fragments.
