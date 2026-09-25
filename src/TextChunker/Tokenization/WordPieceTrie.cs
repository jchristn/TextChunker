namespace TextChunker.Tokenization
{
    using System.Collections.Generic;

    /// <summary>
    /// A character trie over WordPiece vocabulary pieces that finds the longest piece starting at a position in one
    /// walk, without allocating candidate substrings. Edges are held in a single dictionary keyed by the parent node
    /// and the character, which keeps the structure compact for a vocabulary of tens of thousands of pieces.
    /// Immutable once built and safe for concurrent reads.
    /// </summary>
    internal sealed class WordPieceTrie
    {
        private readonly Dictionary<long, int> _Edges = new Dictionary<long, int>();
        private readonly List<int> _Terminals = new List<int> { -1 };

        /// <summary>
        /// Add a piece with its vocabulary identifier. The first identifier added for a piece wins.
        /// </summary>
        /// <param name="piece">Piece text, without any continuation prefix.</param>
        /// <param name="id">Vocabulary identifier.</param>
        internal void Add(string piece, int id)
        {
            int node = 0;
            foreach (char value in piece)
            {
                long key = ((long)node << 16) | value;
                if (!_Edges.TryGetValue(key, out int child))
                {
                    child = _Terminals.Count;
                    _Terminals.Add(-1);
                    _Edges[key] = child;
                }

                node = child;
            }

            if (_Terminals[node] < 0) _Terminals[node] = id;
        }

        /// <summary>
        /// Find the longest piece that matches text starting at start and ending at or before end.
        /// </summary>
        /// <param name="text">Text to match against.</param>
        /// <param name="start">Inclusive start index.</param>
        /// <param name="end">Exclusive limit index.</param>
        /// <param name="id">Identifier of the longest match, or -1.</param>
        /// <returns>The exclusive end index of the longest match, or -1 when no piece matches.</returns>
        internal int LongestMatch(string text, int start, int end, out int id)
        {
            id = -1;
            int matchEnd = -1;
            int node = 0;
            for (int i = start; i < end; i++)
            {
                if (!_Edges.TryGetValue(((long)node << 16) | text[i], out node)) break;
                int terminal = _Terminals[node];
                if (terminal >= 0)
                {
                    id = terminal;
                    matchEnd = i + 1;
                }
            }

            return matchEnd;
        }
    }
}
