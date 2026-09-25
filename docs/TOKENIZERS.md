# Tokenizers and budget resolution

TextChunker counts tokens with real tokenizers, not character estimates, and resolves a token budget from
the model you are targeting.

## Families

Three tokenizer families ship out of the box. cl100k and o200k, backed by SharpToken, match OpenAI style
BPE models: cl100k for GPT-3.5, GPT-4, and text-embedding-3, and o200k for GPT-4o, GPT-4.1, the o-series,
and GPT-5. BERT WordPiece matches BERT family embedding models such as MiniLM, MPNet, BGE, E5, GTE, and
Nomic. The WordPiece vocabulary (`bert-base-uncased`) is embedded in the assembly, so token counts are
correct offline with no external file to deploy.

For anything else, `MlTokenizerAdapter` wraps any `Microsoft.ML.Tokenizers` tokenizer, so you can plug in
a Hugging Face JSON tokenizer or a SentencePiece model (Llama, Gemma, T5) and get exact counts for that
model. Construct the tokenizer, wrap it, and pass it to the `Chunker` constructor.

```csharp
Tokenizer hf = /* build a Microsoft.ML.Tokenizers tokenizer from your model files */;
IChunker chunker = new Chunker(new MlTokenizerAdapter(hf));
```

Gemini is approximated today with cl100k at a 2048 budget. Native SentencePiece is reachable now through
`MlTokenizerAdapter` when you supply the model.

## BERT WordPiece

`BertWordPieceTokenizerAdapter` is a port of the Hugging Face `BertTokenizer`: basic tokenization followed
by greedy longest match first WordPiece. That is also what embedding runtimes such as Ollama (llama.cpp)
and sentence-transformers apply, so the local count is the count the runtime enforces. Normalization runs
one code point at a time and records where every normalized character came from, so token offsets always
index the original text, however much the normalizer adds or removes.

The rules, with the defaults for an uncased vocabulary:

- All whitespace separates words: space, `\n`, `\r`, `\t`, `\v`, `\f`, NBSP, and the Unicode line and
  paragraph separators. A line break never fuses the words around it.
- Control characters, zero width and other format characters (soft hyphen, ZWJ, BOM), and U+FFFD are
  removed.
- Text is lower cased, decomposed, and stripped of nonspacing marks, so `café` and `Türkçe` match the
  unaccented vocabulary instead of becoming `[UNK]`.
- ASCII punctuation and symbols (`$ + = < > | ~ ^` and the rest) and Unicode punctuation become their own
  tokens. Each CJK ideograph is its own word.
- A word that cannot be segmented, such as an emoji or a word containing one, becomes a single `[UNK]`.
- A long word is always segmented in full. Hugging Face collapses a word over 100 characters to one
  `[UNK]`, but llama.cpp segments it, so a long URL or encoded blob can cost dozens of tokens there.

Measured against Ollama `all-minilm` over 43 inputs that exercise every rule (accents, all whitespace
kinds, control characters, emoji and ZWJ sequences, currency and math symbols, CJK, kana, Cyrillic,
Arabic, Greek, astral characters, long words, markdown, code, and JSON), 42 counts match exactly. The one
difference is Devanagari, where llama.cpp also strips spacing combining marks and so counts fewer tokens
than Hugging Face; the adapter follows Hugging Face and counts more. The fixture lives in
`src/Test.Shared/Fixtures/wordpiece-counts.json` and the test suite requires every local count to be at
least the runtime count.

`WordPieceOptions` configures the behavior:

| Option | Default | Meaning |
|---|---|---|
| `LowerCase` | true | Lower case before lookup. Required for uncased vocabularies. |
| `StripAccents` | true | Decompose and remove nonspacing marks. |
| `TokenizeCjkCharacters` | true | Treat each CJK ideograph as its own word. |
| `MaxInputCharactersPerWord` | 0 | Collapse longer words to one unknown token. 0 disables the limit. |
| `UnknownToken` | `[UNK]` | The vocabulary entry for an unsegmentable word. |

```csharp
// The embedded bert-base-uncased vocabulary with custom options.
ITokenizerAdapter uncased = new BertWordPieceTokenizerAdapter(new WordPieceOptions { MaxInputCharactersPerWord = 100 });

// A cased vocabulary supplied as a vocab.txt stream.
using FileStream vocab = File.OpenRead("bert-base-cased-vocab.txt");
ITokenizerAdapter cased = new BertWordPieceTokenizerAdapter(vocab, new WordPieceOptions { LowerCase = false, StripAccents = false });
```

## Resolution

The chunker resolves a `ResolvedTokenizationProfile` once per call using a fixed precedence:

1. An explicit override. Setting `TokenizerKind` to a concrete family or setting `EffectiveInputBudget`
   forces the choice.
2. A calibration probe, if one is configured and calibration is allowed.
3. A provider default, chosen from `ModelId` and `ApiFormat`.
4. The global fallback, cl100k at 8192.

Within the provider default step, a configurable known-model registry is consulted first. The `ModelId`
is normalized (a provider path prefix such as `sentence-transformers/` and a version or quantization tag
such as `:latest` or `:v1.5` are stripped) and matched against the registry, longest key first, so a
specific entry wins over a generic one. A match takes precedence over `ApiFormat`, since the model
identifier is the more authoritative signal. The registry ships seeded with common embedding models:
`nomic-embed-text` (WordPiece, 2048), `all-minilm` (WordPiece, 256), `all-mpnet-base-v2` (WordPiece, 384),
`bge`, `gte`, `e5`, and `mxbai-embed-large` (WordPiece, 512), `bge-m3` (WordPiece, 8192), and the OpenAI
`text-embedding-3-*` and `text-embedding-ada-002` models (cl100k, 8191). Add or override entries with
`TokenizationDefaults.RegisterKnownModel` or by mutating `TokenizationDefaults.KnownModels`.

A known-model match resolves with `ProfileSource = KnownModel` and `UsedFallback = false`, marking it an
authoritative configuration rather than a guess. When no known-model entry matches, the API format defaults
apply: OpenAI and vLLM to cl100k at 8192, Gemini to cl100k at 2048, and BERT family model names to WordPiece
at 512. These report `ProfileSource = ProviderDefault` with `UsedFallback = true`. BERT family detection
matches `bert`, `minilm`, `mpnet`, `nomic`, `mxbai`, `e5`, `gte`, and `bge`.

BERT family WordPiece models reserve 2 tokens (`TokenizationDefaults.BertReservedInputTokens`) for the
`[CLS]` and `[SEP]` special tokens the embedding endpoint adds. The local tokenizer does not count these,
so the effective budget is the model's sequence length minus 2 (for example `all-minilm` resolves to a
256 token model with a 254 token effective budget). cl100k and o200k models reserve nothing.

The budget actually enforced while chunking is the smaller of your `MaxTokens` and the model's resolved
effective budget less any safety margin, so a chunk never exceeds either limit. If you set a
`ContextPrefix`, its token cost is measured once and subtracted from the working budget so a prefixed chunk
still fits.

## Local counts are an approximation of the runtime

A local tokenizer can only match the runtime's tokenizer as closely as it reproduces it. The WordPiece
adapter matches Ollama and Hugging Face on the inputs above, but some configurations still diverge:

- A model mapped to `bert-base-uncased` that actually uses a different vocabulary. The multilingual E5
  models, for example, use an XLM-R SentencePiece tokenizer. Wrap the real tokenizer in
  `MlTokenizerAdapter` for exact counts.
- Runtimes that normalize differently from Hugging Face, such as the Devanagari case above, or that add
  instruction prefixes (`search_document: `) the chunker does not see. Precomposed Korean Hangul syllables
  are another: accent stripping decomposes each into jamo, as Hugging Face does, which costs one token more
  per syllable than Ollama charges. In both cases the local count is the higher one.
- cl100k used as an approximation for a model with its own tokenizer, such as Gemini.

For those cases, hold back a margin and handle an overflow response from the runtime:

```csharp
ChunkingOptions options = new ChunkingOptions
{
    ModelId = "all-minilm",
    SafetyMarginPercentage = 0.04, // 4 percent of the 254 token budget, rounded up: 11 tokens
    SafetyMarginTokens = 0         // an absolute margin, added to the percentage
};
```

The margin comes off the resolved model budget, not off `MaxTokens`, so a `MaxTokens` already below the
adjusted model budget is unaffected. `ChunkToResultAsync` reports the enforced budget in
`Diagnostic.EffectiveTokenBudget`. `ITokenizerCalibrationProbe` measures the budget as a single number, so
it cannot detect per text divergence; a margin covers that.

## How chunks are cut

The chunker does not slice by token index. Every text strategy works on spans of the original text: the
token window splits the text into whitespace delimited words (and each CJK ideograph), counts each word,
packs whole words up to the budget, and then verifies the actual count of the candidate span, galloping
and binary searching until the largest span that fits is found. A chunk is therefore always an exact
substring of the source with known offsets, and a limit of 512 tokens is a hard guarantee rather than an
estimate. A word that alone exceeds the budget is split at grapheme cluster boundaries, never inside a
surrogate pair, an emoji sequence, or a combining mark sequence. A single code point that exceeds the
budget by itself (possible only with a budget of one or two tokens) is kept whole rather than corrupted.

`ITokenizerAdapter.SliceByTokenRange` is still part of the adapter contract for callers that need it, but
the chunker does not use it. The shipped adapters return the exact original text covered by the token
range.

## Live calibration

If you serve models behind an endpoint whose true accepted budget differs from the provider default,
implement `ITokenizerCalibrationProbe`. The resolver calls it with the provisional profile and a tokenizer
and takes the discovered budget. The core library performs no network access itself,
so the transport stays in your code and the package stays dependency light.

```csharp
public sealed class MyProbe : ITokenizerCalibrationProbe
{
    public async Task<TokenizationCalibrationResult?> CalibrateAsync(
        ResolvedTokenizationProfile profile, ITokenizerAdapter tokenizer, CancellationToken token)
    {
        int budget = await DiscoverBudgetAsync(tokenizer, token);
        return new TokenizationCalibrationResult { EffectiveInputBudget = budget };
    }
}

IChunker chunker = new Chunker(null, new MyProbe());
```
