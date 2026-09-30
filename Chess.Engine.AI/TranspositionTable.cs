using System;
using Chess.Engine.Core;

namespace Chess.Engine.AI
{
    public enum TTFlag : byte
    {
        Exact = 0,
        LowerBound = 1, // Beta cutoff / fail-high: actual score >= stored value
        UpperBound = 2  // All-node / fail-low: actual score <= stored value
    }

    public struct TTEntry
    {
        public ulong Key;       // Full Zobrist hash
        public int Score;       // Evaluation score
        public int Depth;       // Search depth
        public Move BestMove;   // Best move in this position
        public TTFlag Flag;     // Exact, LowerBound, UpperBound
    }

    public class TranspositionTable
    {
        private const int Size = 1 << 23; // 8,388,608 entries
        private readonly ulong _sizeMask = Size - 1;
        private readonly TTEntry[] _entries = new TTEntry[Size];

        public const int TT_MISS = -1_000_000;
        public const int MATE_VALUE = 29000;

        public void Clear()
        {
            Array.Clear(_entries, 0, _entries.Length);
        }

        public void Store(ulong hash, int score, int depth, Move move, TTFlag flag, int ply)
        {
            int index = (int)(hash & _sizeMask);

            // Adjust mate scores relative to root distance (ply) to make them position independent
            int storedScore = score;
            if (score > MATE_VALUE)
            {
                storedScore += ply;
            }
            else if (score < -MATE_VALUE)
            {
                storedScore -= ply;
            }

            // Depth-preferred replacement scheme
            if (_entries[index].Key != hash || depth >= _entries[index].Depth)
            {
                _entries[index] = new TTEntry
                {
                    Key = hash,
                    Score = storedScore,
                    Depth = depth,
                    BestMove = move,
                    Flag = flag
                };
            }
        }

        public int Probe(ulong hash, int depth, int alpha, int beta, int ply, out Move move)
        {
            move = Move.None;
            int index = (int)(hash & _sizeMask);
            TTEntry entry = _entries[index];

            if (entry.Key == hash)
            {
                move = entry.BestMove;
                if (entry.Depth >= depth)
                {
                    // Restore stored mate score relative to current ply
                    int score = entry.Score;
                    if (score > MATE_VALUE)
                    {
                        score -= ply;
                    }
                    else if (score < -MATE_VALUE)
                    {
                        score += ply;
                    }

                    if (entry.Flag == TTFlag.Exact)
                    {
                        return score;
                    }
                    if (entry.Flag == TTFlag.LowerBound && score >= beta)
                    {
                        return score;
                    }
                    if (entry.Flag == TTFlag.UpperBound && score <= alpha)
                    {
                        return score;
                    }
                }
            }
            return TT_MISS;
        }
    }
}
