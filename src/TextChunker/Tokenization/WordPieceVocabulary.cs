namespace TextChunker.Tokenization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// An immutable WordPiece vocabulary loaded from a one token per line vocab.txt file. Word initial pieces and
    /// "##" continuation pieces are held in separate lookups so a candidate piece needs a single substring.
    /// </summary>
    internal sealed class WordPieceVocabulary
    {
        private const string _ContinuationPrefix = "##";

        private readonly Dictionary<string, int> _Initial;
        private readonly Dictionary<string, int> _Continuation;
        private readonly string[] _Tokens;

        /// <summary>
        /// Number of entries in the vocabulary.
        /// </summary>
        internal int Count => _Tokens.Length;

        /// <summary>
        /// Length in characters of the longest piece, excluding the continuation prefix.
        /// </summary>
        internal int MaxPieceLength { get; }

        private WordPieceVocabulary(string[] tokens)
        {
            _Tokens = tokens;
            _Initial = new Dictionary<string, int>(tokens.Length, StringComparer.Ordinal);
            _Continuation = new Dictionary<string, int>(tokens.Length / 4, StringComparer.Ordinal);

            int maxLength = 1;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token.Length > _ContinuationPrefix.Length && token.StartsWith(_ContinuationPrefix, StringComparison.Ordinal))
                {
                    string piece = token.Substring(_ContinuationPrefix.Length);
                    if (!_Continuation.ContainsKey(piece)) _Continuation[piece] = i;
                    if (piece.Length > maxLength) maxLength = piece.Length;
                }
                else
                {
                    if (!_Initial.ContainsKey(token)) _Initial[token] = i;
                    if (token.Length > maxLength) maxLength = token.Length;
                }
            }

            MaxPieceLength = maxLength;
        }

        /// <summary>
        /// Load a vocabulary from a stream of UTF-8 text with one token per line. Line order defines the identifiers.
        /// </summary>
        /// <param name="stream">Readable vocabulary stream.</param>
        /// <returns>The loaded vocabulary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the stream holds no tokens.</exception>
        internal static WordPieceVocabulary Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            List<string> tokens = new List<string>(32000);
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                    tokens.Add(line.TrimEnd('\r'));
            }

            while (tokens.Count > 0 && tokens[tokens.Count - 1].Length == 0)
                tokens.RemoveAt(tokens.Count - 1);

            if (tokens.Count == 0)
                throw new InvalidDataException("The WordPiece vocabulary stream contained no tokens.");

            return new WordPieceVocabulary(tokens.ToArray());
        }

        /// <summary>
        /// Look up a word initial piece.
        /// </summary>
        /// <param name="piece">Candidate piece.</param>
        /// <param name="id">Identifier when found.</param>
        /// <returns>True when the piece is in the vocabulary.</returns>
        internal bool TryGetInitial(string piece, out int id)
        {
            return _Initial.TryGetValue(piece, out id);
        }

        /// <summary>
        /// Look up a continuation piece, supplied without its "##" prefix.
        /// </summary>
        /// <param name="piece">Candidate piece without the prefix.</param>
        /// <param name="id">Identifier when found.</param>
        /// <returns>True when the piece is in the vocabulary.</returns>
        internal bool TryGetContinuation(string piece, out int id)
        {
            return _Continuation.TryGetValue(piece, out id);
        }

        /// <summary>
        /// Return the vocabulary entry for an identifier, or null when out of range.
        /// </summary>
        /// <param name="id">Vocabulary identifier.</param>
        /// <returns>The token text, including any "##" prefix, or null.</returns>
        internal string? GetToken(int id)
        {
            if (id < 0 || id >= _Tokens.Length) return null;
            return _Tokens[id];
        }
    }
}
