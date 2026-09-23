# Tokenizers and budget resolution

TextChunker counts tokens with real tokenizers, not character estimates, and resolves a token budget from
the model you are targeting.

## Families

Three tokenizer families ship out of the box. cl100k and o200k, backed by SharpToken, match OpenAI style
BPE models: cl100k for GPT-3.5, GPT-4, and text-embedding-3, and o200k for GPT-4o, GPT-4.1, the o-series,
and GPT-5. BERT WordPiece, backed by `Microsoft.ML.Tokenizers`, matches BERT family embedding models such
as MiniLM, BGE, E5, and GTE. The WordPiece vocabulary (`bert-base-uncased`) is embedded in the assembly,
so token counts are correct offline with no external file to deploy.

For anything else, `MlTokenizerAdapter` wraps any `Microsoft.ML.Tokenizers` tokenizer, so you can plug in
a Hugging Face JSON tokenizer or a SentencePiece model (Llama, Gemma, T5) and get exact counts for that
model. Construct the tokenizer, wrap it, and pass it to the `Chunker` constructor.

```csharp
Tokenizer hf = /* build a Microsoft.ML.Tokenizers tokenizer from your model files */;
IChunker chunker = new Chunker(new MlTokenizerAdapter(hf));
```

Gemini is approximated today with cl100k at a 2048 budget. Native SentencePiece is reachable now through
`MlTokenizerAdapter` when you supply the model.

## Resolution

The chunker resolves a `ResolvedTokenizationProfile` once per call using a fixed precedence:

1. An explicit override. Setting `TokenizerKind` to a concrete family or setting `EffectiveInputBudget`
   forces the choice.
2. A calibration probe, if one is configured and calibration is allowed.
3. A provider default, chosen from `ApiFormat` and `ModelId`.
4. The global fallback, cl100k at 8192.

Provider defaults map OpenAI and vLLM to cl100k at 8192, Gemini to cl100k at 2048, and BERT family model
names to WordPiece at 512. Model name detection matches `bert`, `minilm`, `e5`, `gte`, and `bge`.

The budget actually enforced while chunking is the smaller of your `MaxTokens` and the model's resolved
budget, so a chunk never exceeds either limit. If you set a `ContextPrefix`, its token cost is measured
once and subtracted from the working budget so a prefixed chunk still fits.

## The strict slice guard

Every token slice is decoded, re encoded, and shrunk until its actual token count is within the budget.
This matters most for WordPiece, where decoding and encoding are not symmetric, and it is the reason a
512 token limit is a hard guarantee rather than an estimate.

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
