namespace TextChunker.Tokenization
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// A BERT WordPiece tokenizer that reproduces the Hugging Face basic tokenizer and greedy longest match first
    /// WordPiece algorithm, and reports every token's character range in the original, unnormalized text.
    /// Normalization (control character removal, lower casing, accent stripping, CJK and punctuation splitting) is
    /// applied one code point at a time while tracking where each normalized character came from, so offsets never
    /// drift regardless of how much the normalizer adds or removes. Safe for concurrent use.
    /// </summary>
    internal sealed class WordPieceTokenizer
    {
        private readonly WordPieceVocabulary _Vocabulary;
        private readonly WordPieceOptions _Options;
        private readonly int _UnknownId;
        private readonly ConcurrentDictionary<int, string> _NormalizedCodePoints = new ConcurrentDictionary<int, string>();

        /// <summary>
        /// Initialize a new tokenizer.
        /// </summary>
        /// <param name="vocabulary">WordPiece vocabulary.</param>
        /// <param name="options">Normalization options. The instance is not copied, so callers pass a private copy.</param>
        /// <exception cref="ArgumentNullException">Thrown when vocabulary or options is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the unknown token is not in the vocabulary.</exception>
        internal WordPieceTokenizer(WordPieceVocabulary vocabulary, WordPieceOptions options)
        {
            _Vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
            _Options = options ?? throw new ArgumentNullException(nameof(options));

            if (!_Vocabulary.TryGetInitial(_Options.UnknownToken, out _UnknownId))
                throw new ArgumentException(
                    "The unknown token '" + _Options.UnknownToken + "' is not present in the WordPiece vocabulary.",
                    nameof(options));
        }

        /// <summary>
        /// The vocabulary backing this tokenizer.
        /// </summary>
        internal WordPieceVocabulary Vocabulary => _Vocabulary;

        /// <summary>
        /// Count the WordPiece tokens in text, excluding the [CLS] and [SEP] special tokens.
        /// </summary>
        /// <param name="text">Text to count.</param>
        /// <returns>Token count.</returns>
        internal int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Run(text, null);
        }

        /// <summary>
        /// Tokenize text into WordPiece tokens with original text offsets, excluding special tokens.
        /// </summary>
        /// <param name="text">Text to tokenize.</param>
        /// <returns>Tokens in order.</returns>
        internal List<WordPieceToken> Tokenize(string text)
        {
            List<WordPieceToken> tokens = new List<WordPieceToken>();
            if (string.IsNullOrEmpty(text)) return tokens;
            Run(text, tokens);
            return tokens;
        }

        private int Run(string text, List<WordPieceToken>? output)
        {
            StringBuilder word = new StringBuilder(32);
            List<int> starts = new List<int>(32);
            List<int> ends = new List<int>(32);
            int wordCodePoints = 0;
            int count = 0;
            int index = 0;

            while (index < text.Length)
            {
                int start = index;
                char current = text[index];
                int codePoint;
                if (char.IsHighSurrogate(current) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    codePoint = char.ConvertToUtf32(current, text[index + 1]);
                    index += 2;
                }
                else
                {
                    codePoint = current;
                    index += 1;
                }

                int end = index;

                if (IsWhitespace(codePoint))
                {
                    count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                    continue;
                }

                if (IsRemoved(codePoint, text, start)) continue;

                if (codePoint < 0x80)
                {
                    char ascii = (char)codePoint;
                    if (_Options.LowerCase && ascii >= 'A' && ascii <= 'Z') ascii = (char)(ascii + 32);

                    if (IsAsciiPunctuation(ascii))
                    {
                        count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                        AppendChar(word, starts, ends, ascii, start, end);
                        wordCodePoints = 1;
                        count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                    }
                    else
                    {
                        AppendChar(word, starts, ends, ascii, start, end);
                        wordCodePoints++;
                    }

                    continue;
                }

                string normalized = NormalizeCodePoint(codePoint);
                if (normalized.Length == 0) continue;

                if (_Options.TokenizeCjkCharacters && IsCjk(codePoint))
                {
                    count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                    AppendString(word, starts, ends, normalized, start, end);
                    wordCodePoints = CountCodePoints(normalized);
                    count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                    continue;
                }

                int position = 0;
                while (position < normalized.Length)
                {
                    int length = char.IsHighSurrogate(normalized[position]) && position + 1 < normalized.Length ? 2 : 1;
                    string piece = normalized.Substring(position, length);
                    position += length;

                    if (IsPunctuation(piece))
                    {
                        count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                        AppendString(word, starts, ends, piece, start, end);
                        wordCodePoints = 1;
                        count += FlushWord(word, starts, ends, ref wordCodePoints, output);
                    }
                    else
                    {
                        AppendString(word, starts, ends, piece, start, end);
                        wordCodePoints++;
                    }
                }
            }

            count += FlushWord(word, starts, ends, ref wordCodePoints, output);
            return count;
        }

        private int FlushWord(StringBuilder word, List<int> starts, List<int> ends, ref int wordCodePoints, List<WordPieceToken>? output)
        {
            if (word.Length == 0)
            {
                wordCodePoints = 0;
                return 0;
            }

            int produced;
            if (_Options.MaxInputCharactersPerWord > 0 && wordCodePoints > _Options.MaxInputCharactersPerWord)
            {
                output?.Add(new WordPieceToken(_UnknownId, starts[0], ends[ends.Count - 1]));
                produced = 1;
            }
            else
            {
                produced = SegmentWord(word.ToString(), starts, ends, output);
            }

            word.Clear();
            starts.Clear();
            ends.Clear();
            wordCodePoints = 0;
            return produced;
        }

        private int SegmentWord(string word, List<int> starts, List<int> ends, List<WordPieceToken>? output)
        {
            // Most words are a single vocabulary entry, and the word string already exists, so try it whole first.
            if (_Vocabulary.TryGetInitial(word, out int wholeId))
            {
                output?.Add(new WordPieceToken(wholeId, starts[0], ends[ends.Count - 1]));
                return 1;
            }

            int rollback = output?.Count ?? 0;
            int produced = 0;
            int start = 0;

            while (start < word.Length)
            {
                int id;
                int end = start == 0
                    ? _Vocabulary.MatchInitial(word, start, word.Length, out id)
                    : _Vocabulary.MatchContinuation(word, start, word.Length, out id);

                if (end < 0)
                {
                    if (output != null)
                    {
                        output.RemoveRange(rollback, output.Count - rollback);
                        output.Add(new WordPieceToken(_UnknownId, starts[0], ends[ends.Count - 1]));
                    }

                    return 1;
                }

                output?.Add(new WordPieceToken(id, starts[start], ends[end - 1]));
                produced++;
                start = end;
            }

            return produced;
        }

        private string NormalizeCodePoint(int codePoint)
        {
            return _NormalizedCodePoints.GetOrAdd(codePoint, NormalizeUncached);
        }

        private string NormalizeUncached(int codePoint)
        {
            string value = char.ConvertFromUtf32(codePoint);
            if (_Options.LowerCase) value = value.ToLowerInvariant();

            if (_Options.StripAccents)
            {
                string decomposed = value.Normalize(NormalizationForm.FormD);
                StringBuilder builder = new StringBuilder(decomposed.Length);
                for (int i = 0; i < decomposed.Length; i++)
                {
                    if (CharUnicodeInfo.GetUnicodeCategory(decomposed, i) == UnicodeCategory.NonSpacingMark)
                    {
                        if (char.IsHighSurrogate(decomposed[i]) && i + 1 < decomposed.Length) i++;
                        continue;
                    }

                    builder.Append(decomposed[i]);
                }

                value = builder.ToString();
            }

            return value;
        }

        private static void AppendChar(StringBuilder word, List<int> starts, List<int> ends, char value, int start, int end)
        {
            word.Append(value);
            starts.Add(start);
            ends.Add(end);
        }

        private static void AppendString(StringBuilder word, List<int> starts, List<int> ends, string value, int start, int end)
        {
            for (int i = 0; i < value.Length; i++)
            {
                word.Append(value[i]);
                starts.Add(start);
                ends.Add(end);
            }
        }

        private static int CountCodePoints(string value)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) i++;
                count++;
            }

            return count;
        }

        private static bool IsWhitespace(int codePoint)
        {
            return codePoint <= 0xFFFF && char.IsWhiteSpace((char)codePoint);
        }

        private static bool IsRemoved(int codePoint, string text, int index)
        {
            if (codePoint == 0 || codePoint == 0xFFFD) return true;

            UnicodeCategory category = codePoint <= 0xFFFF
                ? CharUnicodeInfo.GetUnicodeCategory((char)codePoint)
                : CharUnicodeInfo.GetUnicodeCategory(text, index);

            switch (category)
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.OtherNotAssigned:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAsciiPunctuation(char value)
        {
            return (value >= 33 && value <= 47)
                || (value >= 58 && value <= 64)
                || (value >= 91 && value <= 96)
                || (value >= 123 && value <= 126);
        }

        private static bool IsPunctuation(string codePoint)
        {
            if (codePoint.Length == 1 && codePoint[0] < 0x80) return IsAsciiPunctuation(codePoint[0]);

            switch (CharUnicodeInfo.GetUnicodeCategory(codePoint, 0))
            {
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCjk(int codePoint)
        {
            return (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
                || (codePoint >= 0x3400 && codePoint <= 0x4DBF)
                || (codePoint >= 0x20000 && codePoint <= 0x2A6DF)
                || (codePoint >= 0x2A700 && codePoint <= 0x2B73F)
                || (codePoint >= 0x2B740 && codePoint <= 0x2B81F)
                || (codePoint >= 0x2B820 && codePoint <= 0x2CEAF)
                || (codePoint >= 0xF900 && codePoint <= 0xFAFF)
                || (codePoint >= 0x2F800 && codePoint <= 0x2FA1F);
        }
    }
}
