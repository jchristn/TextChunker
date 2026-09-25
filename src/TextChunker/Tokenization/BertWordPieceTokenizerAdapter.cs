namespace TextChunker.Tokenization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Tokenizer adapter for BERT family WordPiece embedding models such as MiniLM, MPNet, BGE, GTE, E5, and Nomic.
    /// It reproduces the Hugging Face <c>BertTokenizer</c> (basic tokenization followed by greedy longest match first
    /// WordPiece), which is also what embedding runtimes such as Ollama and sentence-transformers apply, so local
    /// token counts match what the runtime enforces. The default constructor uses the bert-base-uncased vocabulary
    /// embedded in the assembly; another vocabulary, for example a cased one, can be supplied as a stream.
    /// Counts exclude the [CLS] and [SEP] special tokens, which the tokenization profile reserves separately.
    /// </summary>
    public class BertWordPieceTokenizerAdapter : ITokenizerAdapter
    {
        private const string _ResourceName = "TextChunker.Tokenization.Data.bert-base-uncased-vocab.txt";

        private static readonly Lazy<WordPieceVocabulary> _EmbeddedVocabulary =
            new Lazy<WordPieceVocabulary>(LoadEmbeddedVocabulary, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<WordPieceTokenizer> _DefaultTokenizer =
            new Lazy<WordPieceTokenizer>(() => new WordPieceTokenizer(_EmbeddedVocabulary.Value, new WordPieceOptions()), LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly WordPieceTokenizer _Tokenizer;
        private readonly WordPieceOptions _Options;

        /// <summary>
        /// Initialize an adapter over the embedded bert-base-uncased vocabulary with default options.
        /// </summary>
        public BertWordPieceTokenizerAdapter()
        {
            _Tokenizer = _DefaultTokenizer.Value;
            _Options = new WordPieceOptions();
        }

        /// <summary>
        /// Initialize an adapter over the embedded bert-base-uncased vocabulary with the supplied options.
        /// </summary>
        /// <param name="options">Normalization options. The instance is copied.</param>
        /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
        public BertWordPieceTokenizerAdapter(WordPieceOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _Options = options.Clone();
            _Tokenizer = new WordPieceTokenizer(_EmbeddedVocabulary.Value, _Options.Clone());
        }

        /// <summary>
        /// Initialize an adapter over a caller supplied vocab.txt (one token per line, line order defines the
        /// identifiers), for example the vocabulary of a cased BERT model.
        /// </summary>
        /// <param name="vocabulary">Readable vocabulary stream. The stream is read fully and left open.</param>
        /// <param name="options">Normalization options, or null for defaults. The instance is copied. Set
        /// <see cref="WordPieceOptions.LowerCase"/> and <see cref="WordPieceOptions.StripAccents"/> to false for a
        /// cased vocabulary.</param>
        /// <exception cref="ArgumentNullException">Thrown when vocabulary is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the vocabulary stream holds no tokens.</exception>
        /// <exception cref="ArgumentException">Thrown when the unknown token is not in the vocabulary.</exception>
        public BertWordPieceTokenizerAdapter(Stream vocabulary, WordPieceOptions? options = null)
        {
            if (vocabulary == null) throw new ArgumentNullException(nameof(vocabulary));
            _Options = options?.Clone() ?? new WordPieceOptions();
            _Tokenizer = new WordPieceTokenizer(WordPieceVocabulary.Load(vocabulary), _Options.Clone());
        }

        /// <summary>
        /// A copy of the normalization options in effect for this adapter.
        /// </summary>
        public WordPieceOptions Options => _Options.Clone();

        /// <inheritdoc />
        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return _Tokenizer.CountTokens(text);
        }

        /// <inheritdoc />
        public IReadOnlyList<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
            return _Tokenizer.Tokenize(text).Select(token => token.Id).ToArray();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an identifier is not in the vocabulary.</exception>
        public string Decode(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));

            StringBuilder builder = new StringBuilder();
            foreach (int id in tokenIds)
            {
                string? token = _Tokenizer.Vocabulary.GetToken(id);
                if (token == null)
                    throw new ArgumentOutOfRangeException(nameof(tokenIds), "Token identifier " + id + " is not in the WordPiece vocabulary.");

                if (token.Length > 2 && token.StartsWith("##", StringComparison.Ordinal))
                {
                    builder.Append(token, 2, token.Length - 2);
                }
                else
                {
                    if (builder.Length > 0) builder.Append(' ');
                    builder.Append(token);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Return the span of the original text covered by a range of tokens. The result is always a substring of the
        /// input, so it never contains normalized characters and never splits a surrogate pair.
        /// </summary>
        /// <param name="text">Source text.</param>
        /// <param name="startTokenIndex">Zero based token index to start from. Must be at least 0.</param>
        /// <param name="tokenCount">Number of tokens to include. Values of 0 or less return an empty string.</param>
        /// <returns>The original text from the first token's start to the last token's end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when startTokenIndex is negative.</exception>
        public string SliceByTokenRange(string text, int startTokenIndex, int tokenCount)
        {
            if (startTokenIndex < 0) throw new ArgumentOutOfRangeException(nameof(startTokenIndex));
            if (string.IsNullOrEmpty(text) || tokenCount <= 0) return string.Empty;

            List<WordPieceToken> tokens = _Tokenizer.Tokenize(text);
            if (startTokenIndex >= tokens.Count) return string.Empty;

            int last = Math.Min(tokens.Count, startTokenIndex + tokenCount) - 1;
            int start = tokens[startTokenIndex].Start;
            int end = Math.Max(start, tokens[last].End);
            return text.Substring(start, end - start);
        }

        private static WordPieceVocabulary LoadEmbeddedVocabulary()
        {
            Assembly assembly = typeof(BertWordPieceTokenizerAdapter).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(_ResourceName);
            if (stream == null)
                throw new InvalidOperationException("Unable to load embedded tokenizer vocabulary: " + _ResourceName);

            return WordPieceVocabulary.Load(stream);
        }
    }
}
