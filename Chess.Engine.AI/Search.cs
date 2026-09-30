using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Chess.Engine.Core;

namespace Chess.Engine.AI
{
    // ──────────────────────────────────────────────────────────────────
    //  Public result / info types
    // ──────────────────────────────────────────────────────────────────

    public sealed class SearchResult
    {
        public Move BestMove { get; set; }
        public int Score { get; set; }
        public int Depth { get; set; }
        public long Nodes { get; set; }
        public long TimeMs { get; set; }
    }

    public sealed class SearchInfo
    {
        public int Depth { get; set; }
        public int Score { get; set; }
        public long Nodes { get; set; }
        public long TimeMs { get; set; }
        public string Pv { get; set; } = string.Empty;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Search engine
    // ──────────────────────────────────────────────────────────────────

    public sealed class Search
    {
        // ── Constants ────────────────────────────────────────────────
        private const int MaxPly = 128;
        private const int MaxMoves = 256;
        private const int Infinity = 30_000;
        private const int MateScore = 29_000;       // matches TT
        private const int NullMoveR = 3;
        private const int AspirationDelta = 50;
        private const int FutilityDepthLimit = 2;
        private const int LmrMinDepth = 3;
        private const int LmrMinMoveIndex = 4;
        private const int NodeCheckInterval = 2048;

        // Futility margins indexed by depth (1..2)
        private static readonly int[] FutilityMargins = { 0, 200, 400 };

        // ── Piece value lookup for MVV-LVA / SEE (indexed by PieceType 0-12) ──
        private static readonly int[] PieceValues =
        {
            100, 320, 330, 500, 900, 20_000,  // White P,N,B,R,Q,K
            100, 320, 330, 500, 900, 20_000,  // Black P,N,B,R,Q,K
            0                                   // None
        };

        // Pre-allocated search state (zero heap in hot path) ───────
        private readonly TranspositionTable _tt;

        // Killer moves: 2 killers per ply
        private readonly Move[] _killers = new Move[MaxPly * 2];

        // Countermove table
        private readonly Move[,] _counterMoves = new Move[12, 64]; // [pieceType, toSquare]

        // Repetition history tracking
        private readonly ulong[] _hashHistory = new ulong[1024];
        private int _hashHistoryCount = 0;

        // History heuristic: [side 0/1][from][to]
        private readonly int[,,] _history = new int[2, 64, 64];

        // PV table (triangular)
        private readonly Move[] _pvTable = new Move[MaxPly * MaxPly];
        private readonly int[] _pvLength = new int[MaxPly];

        // Search counters / time control
        private long _nodes;
        private bool _stopped;
        private long _stopTimeMs;
        private readonly Stopwatch _timer = new();

        // ── Event ────────────────────────────────────────────────────
        public event Action<SearchInfo>? OnSearchProgress;

        // ── Constructor ──────────────────────────────────────────────
        public Search(TranspositionTable tt)
        {
            _tt = tt;
        }

        // ──────────────────────────────────────────────────────────────
        //  Public entry point
        // ──────────────────────────────────────────────────────────────

        public SearchResult FindBestMove(Board board, int maxDepth, int maxTimeMs)
        {
            // Reset state
            _nodes = 0;
            _stopped = false;
            Array.Clear(_killers);
            Array.Clear(_history);
            Array.Clear(_counterMoves);

            _hashHistoryCount = 0;
            _hashHistory[_hashHistoryCount++] = board.ZobristHash;

            _timer.Restart();
            _stopTimeMs = maxTimeMs > 0 ? maxTimeMs : long.MaxValue;

            Move bestMove = Move.None;
            int bestScore = -Infinity;
            int completedDepth = 0;

            // ── Iterative Deepening ──────────────────────────────────
            int alpha = -Infinity;
            int beta = Infinity;

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                int score = AlphaBeta(board, depth, alpha, beta, 0, true, Move.None);

                if (_stopped)
                    break;

                // ── Aspiration window re-search ──────────────────────
                if (score <= alpha || score >= beta)
                {
                    // Fell outside the window → full re-search
                    alpha = -Infinity;
                    beta = Infinity;
                    score = AlphaBeta(board, depth, alpha, beta, 0, true, Move.None);

                    if (_stopped)
                        break;
                }

                // Iteration completed successfully
                bestScore = score;
                completedDepth = depth;

                // Extract PV move
                if (_pvLength[0] > 0)
                    bestMove = _pvTable[0];

                // Set aspiration window for next iteration
                alpha = score - AspirationDelta;
                beta = score + AspirationDelta;

                // Report progress
                long elapsed = _timer.ElapsedMilliseconds;
                string pv = BuildPvString(0);

                OnSearchProgress?.Invoke(new SearchInfo
                {
                    Depth = depth,
                    Score = bestScore,
                    Nodes = _nodes,
                    TimeMs = elapsed,
                    Pv = pv
                });

                // Age history: halve all values to give recent entries more weight
                for (int s = 0; s < 2; s++)
                    for (int f = 0; f < 64; f++)
                        for (int t = 0; t < 64; t++)
                            _history[s, f, t] >>= 1;
            }

            _timer.Stop();

            return new SearchResult
            {
                BestMove = bestMove,
                Score = bestScore,
                Depth = completedDepth,
                Nodes = _nodes,
                TimeMs = _timer.ElapsedMilliseconds
            };
        }

        // ──────────────────────────────────────────────────────────────
        //  Alpha-Beta with PVS
        // ──────────────────────────────────────────────────────────────

        private int AlphaBeta(Board board, int depth, int alpha, int beta, int ply, bool isPvNode, Move lastMove)
        {
            // ── Time check ───────────────────────────────────────────
            if ((_nodes & (NodeCheckInterval - 1)) == 0 && _timer.ElapsedMilliseconds >= _stopTimeMs)
            {
                _stopped = true;
                return 0;
            }

            _pvLength[ply] = 0;

            bool isRoot = ply == 0;

            // ── Mate Distance Pruning ────────────────────────────────
            if (!isRoot)
            {
                int mateAlpha = Math.Max(alpha, -MateScore + ply);
                int mateBeta = Math.Min(beta, MateScore - ply - 1);
                if (mateAlpha >= mateBeta)
                    return mateAlpha;
                alpha = mateAlpha;
                beta = mateBeta;
            }

            int originalAlpha = alpha;

            // ── Draw by 50-move rule ─────────────────────────────────
            if (!isRoot && board.HalfMoveClock >= 100)
                return 0;

            // ── Repetition Detection ──────────────────────────────────
            if (!isRoot && IsRepetition(board.ZobristHash, ply))
                return 0;

            // ── Quiescence at depth 0 ────────────────────────────────
            if (depth <= 0)
                return Quiescence(board, alpha, beta, ply);

            _nodes++;

            bool inCheck = board.IsInCheck(board.WhiteToMove);

            // Check extension
            if (inCheck)
                depth++;

            // ── TT Probe ─────────────────────────────────────────────
            Move ttMove = Move.None;
            int ttScore = _tt.Probe(board.ZobristHash, depth, alpha, beta, ply, out ttMove);
            if (!isPvNode && ttScore != TranspositionTable.TT_MISS)
                return ttScore;

            // Internal Iterative Reduction (IIR)
            if (ttMove == Move.None && depth >= 4)
                depth--;

            int staticEval = Evaluation.Evaluate(board);

            // Razoring: at very low depths, if static eval is far below alpha,
            // drop straight into quiescence rather than wasting time on a full search
            if (!isPvNode && !inCheck && depth <= 2)
            {
                int razorMargin = depth == 1 ? 300 : 600;
                if (staticEval + razorMargin <= alpha)
                {
                    int qScore = Quiescence(board, alpha, beta, ply);
                    if (qScore <= alpha)
                        return qScore;
                }
            }

            // ── Null Move Pruning ────────────────────────────────────
            if (!isPvNode && !inCheck && depth >= NullMoveR + 1 && HasNonPawnMaterial(board))
            {
                // Make null move (pass the turn)
                var nullState = board.MakeNullMove();
                int nullScore = -AlphaBeta(board, depth - 1 - NullMoveR, -beta, -beta + 1, ply + 1, false, Move.None);
                board.UnmakeNullMove(nullState);

                if (_stopped) return 0;

                if (nullScore >= beta)
                    return beta; // null-move cutoff
            }

            // ── Futility Pruning flag ────────────────────────────────
            bool canFutilityPrune = !isPvNode && !inCheck && depth <= FutilityDepthLimit
                                    && staticEval + FutilityMargins[depth] <= alpha;

            // ── Generate moves ───────────────────────────────────────
            Span<Move> localMoves = stackalloc Move[MaxMoves];
            int moveCount = MoveGenerator.GenerateMoves(board, localMoves, false);

            // Checkmate / Stalemate
            if (moveCount == 0)
            {
                return inCheck ? -MateScore + ply : 0;
            }

            // ── Move ordering ────────────────────────────────────────
            Span<int> scores = stackalloc int[moveCount];
            ScoreMoves(localMoves, moveCount, scores, ttMove, ply, board, lastMove);

            Move bestMove = Move.None;
            int bestScore = -Infinity;
            int sideIndex = board.WhiteToMove ? 0 : 1;
            int movesSearched = 0;

            for (int i = 0; i < moveCount; i++)
            {
                // Selection sort: find best-scored move at position i
                PickMove(localMoves, scores, i, moveCount);
                Move move = localMoves[i];

                bool isCapture = move.IsCapture;
                bool isPromotion = move.IsPromotion;
                bool isKiller = move == _killers[ply * 2] || move == _killers[ply * 2 + 1];
                bool isQuiet = !isCapture && !isPromotion;

                // ── Futility Pruning (quiet moves only) ──────────────
                if (canFutilityPrune && isQuiet && movesSearched > 0 && !isKiller)
                    continue;

                var state = board.MakeMove(move);
                _hashHistory[_hashHistoryCount++] = board.ZobristHash;

                int score;

                // ── PVS: null-window search for non-first moves ──────
                if (movesSearched == 0)
                {
                    // First move: full window
                    score = -AlphaBeta(board, depth - 1, -beta, -alpha, ply + 1, isPvNode, move);
                }
                else
                {
                    // ── Late Move Reductions ─────────────────────────
                    int reduction = 0;
                    if (depth >= LmrMinDepth && movesSearched >= LmrMinMoveIndex
                        && !inCheck && isQuiet && !isKiller)
                    {
                        reduction = (int)(Math.Log(depth) * Math.Log(movesSearched + 1) / 2.0);
                        reduction = Math.Clamp(reduction, 1, depth - 2);
                    }

                    // Null window search with possible reduction
                    score = -AlphaBeta(board, depth - 1 - reduction, -alpha - 1, -alpha, ply + 1, false, move);

                    // Re-search at full depth if reduced search returned > alpha
                    if (reduction > 0 && score > alpha)
                        score = -AlphaBeta(board, depth - 1, -alpha - 1, -alpha, ply + 1, false, move);

                    // Re-search with full window if null-window failed high in PV node
                    if (isPvNode && score > alpha && score < beta)
                        score = -AlphaBeta(board, depth - 1, -beta, -alpha, ply + 1, isPvNode, move);
                }

                _hashHistoryCount--;
                board.UnmakeMove(move, state);

                if (_stopped) return 0;

                movesSearched++;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;

                    if (score > alpha)
                    {
                        alpha = score;

                        // Update PV
                        UpdatePv(ply, move);

                        if (score >= beta)
                        {
                            // ── Beta cutoff bookkeeping ──────────────
                            if (isQuiet)
                            {
                                // Update killers
                                if (_killers[ply * 2] != move)
                                {
                                    _killers[ply * 2 + 1] = _killers[ply * 2];
                                    _killers[ply * 2] = move;
                                }

                                // Update history
                                _history[sideIndex, move.From, move.To] += depth * depth;

                                // After updating killers, store countermove
                                if (lastMove != Move.None)
                                {
                                    int prevPiece = (int)board.GetPieceAt(lastMove.To);
                                    if (prevPiece >= 0 && prevPiece < 12)
                                        _counterMoves[prevPiece, lastMove.To] = move;
                                }
                            }

                            _tt.Store(board.ZobristHash, score, depth, move, TTFlag.LowerBound, ply);
                            return score;
                        }
                    }
                }
            }

            // ── TT Store ─────────────────────────────────────────────
            TTFlag flag = bestScore <= originalAlpha ? TTFlag.UpperBound : TTFlag.Exact;
            _tt.Store(board.ZobristHash, bestScore, depth, bestMove, flag, ply);
            return bestScore;
        }

        // ──────────────────────────────────────────────────────────────
        //  Quiescence Search
        // ──────────────────────────────────────────────────────────────

        private int Quiescence(Board board, int alpha, int beta, int ply)
        {
            if ((_nodes & (NodeCheckInterval - 1)) == 0 && _timer.ElapsedMilliseconds >= _stopTimeMs)
            {
                _stopped = true;
                return 0;
            }

            _nodes++;

            bool inCheck = board.IsInCheck(board.WhiteToMove);

            int standPat = Evaluation.Evaluate(board);

            if (!inCheck)
            {
                if (standPat >= beta)
                    return beta;
                if (standPat > alpha)
                    alpha = standPat;
            }

            // In check: generate ALL legal moves (evasions); otherwise captures only
            Span<Move> localMoves = stackalloc Move[MaxMoves];
            int moveCount = MoveGenerator.GenerateMoves(board, localMoves, !inCheck);

            // In check with no moves = checkmate
            if (inCheck && moveCount == 0)
                return -MateScore + ply;

            // Score and sort captures
            Span<int> scores = stackalloc int[moveCount];
            ScoreQMoves(localMoves, moveCount, scores, board);

            for (int i = 0; i < moveCount; i++)
            {
                PickMove(localMoves, scores, i, moveCount);
                Move move = localMoves[i];

                // Delta pruning: skip if even the best gain can't reach alpha
                const int DeltaMargin = 200;
                if (!inCheck && move.IsCapture)
                {
                    int capturedVal = move.Captured != PieceType.None ? PieceValues[(int)move.Captured] : 0;
                    if (standPat + capturedVal + DeltaMargin <= alpha)
                        continue;
                }

                // SEE pruning: skip losing captures (not when in check)
                if (!inCheck && move.IsCapture && !SEE(board, move, 0))
                    continue;

                var state = board.MakeMove(move);
                int score = -Quiescence(board, -beta, -alpha, ply + 1);
                board.UnmakeMove(move, state);

                if (_stopped) return 0;

                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }

            return alpha;
        }

        // ──────────────────────────────────────────────────────────────
        //  Static Exchange Evaluation (SEE)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the exchange on move's target square is >= threshold.
        /// Uses the "swap algorithm" with attack bitboards.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SEE(Board board, Move move, int threshold)
        {
            int from = move.From;
            int to = move.To;

            // Initial gain is the captured piece value
            int gain = move.Captured != PieceType.None ? PieceValues[(int)move.Captured] : 0;

            // Quick check: if gain - threshold is already losing even without
            // considering our piece being captured, it's definitely good
            int value = gain - threshold;
            if (value < 0) return false;

            // Now assume the moving piece can be captured
            PieceType movingPiece = board.GetPieceAt(from);
            value -= PieceValues[(int)movingPiece];
            if (value >= 0) return true; // Even if we lose our piece, we're still ahead

            // Full SEE swap
            ulong occupied = board.Occupied ^ (1UL << from) ^ (1UL << to);
            if (move.Flag == MoveFlag.EnPassant)
            {
                int epCapSq = board.WhiteToMove ? to - 8 : to + 8;
                occupied ^= 1UL << epCapSq;
            }

            ulong attackers = GetAllAttackers(board, to, occupied);
            bool sideToMove = !board.WhiteToMove; // Opponent gets to recapture first

            // Swap list
            Span<int> swapList = stackalloc int[32];
            int swapCount = 0;
            swapList[swapCount++] = gain; // initial capture
            int currentGain = PieceValues[(int)movingPiece]; // piece that just landed on 'to'

            while (true)
            {
                ulong sideAttackers = attackers & (sideToMove ? board.WhiteOccupied : board.BlackOccupied);
                if (sideAttackers == 0) break;

                // Pick Least Valuable Attacker (LVA)
                int lvaType = -1;
                int startIdx = sideToMove ? 0 : 6;
                ulong lvaSquareBB = 0;

                // Pawns
                ulong bb = board.Pieces[startIdx + 0] & sideAttackers & occupied;
                if (bb != 0) { lvaType = startIdx + 0; lvaSquareBB = bb & (~bb + 1); goto found; }
                // Knights
                bb = board.Pieces[startIdx + 1] & sideAttackers & occupied;
                if (bb != 0) { lvaType = startIdx + 1; lvaSquareBB = bb & (~bb + 1); goto found; }
                // Bishops
                bb = board.Pieces[startIdx + 2] & sideAttackers & occupied;
                if (bb != 0) { lvaType = startIdx + 2; lvaSquareBB = bb & (~bb + 1); goto found; }
                // Rooks
                bb = board.Pieces[startIdx + 3] & sideAttackers & occupied;
                if (bb != 0) { lvaType = startIdx + 3; lvaSquareBB = bb & (~bb + 1); goto found; }
                // Queens
                bb = board.Pieces[startIdx + 4] & sideAttackers & occupied;
                if (bb != 0) { lvaType = startIdx + 4; lvaSquareBB = bb & (~bb + 1); goto found; }
                // King
                bb = board.Pieces[startIdx + 5] & sideAttackers & occupied;
                if (bb != 0) { lvaType = startIdx + 5; lvaSquareBB = bb & (~bb + 1); goto found; }

                break; // no attackers

            found:
                swapList[swapCount] = -swapList[swapCount - 1] + currentGain;
                swapCount++;
                currentGain = PieceValues[lvaType];

                // Remove attacker from occupied
                occupied ^= lvaSquareBB;

                // If capturing with king and opponent still has attackers, stop
                if ((lvaType % 6) == 5)
                {
                    // King captured — check if other side still attacks
                    ulong nextSideAttackers = GetAllAttackers(board, to, occupied)
                                              & (sideToMove ? board.BlackOccupied : board.WhiteOccupied)
                                              & occupied;
                    if (nextSideAttackers != 0)
                    {
                        // King can't actually capture — undo last swap entry
                        swapCount--;
                    }
                    break;
                }

                // Reveal new x-ray attackers through the removed piece
                attackers |= RevealXrayAttackers(board, to, occupied, lvaSquareBB);

                sideToMove = !sideToMove;
            }

            // Negamax the swap list from the back
            while (--swapCount > 0)
            {
                swapList[swapCount - 1] = Math.Min(-swapList[swapCount], swapList[swapCount - 1]);
            }

            return swapList[0] >= threshold;
        }

        private static ulong GetAllAttackers(Board board, int sq, ulong occupied)
        {
            return (Bitboards.PawnAttacks[1][sq] & board.Pieces[(int)PieceType.WhitePawn])
                 | (Bitboards.PawnAttacks[0][sq] & board.Pieces[(int)PieceType.BlackPawn])
                 | (Bitboards.KnightAttacks[sq] & (board.Pieces[(int)PieceType.WhiteKnight] | board.Pieces[(int)PieceType.BlackKnight]))
                 | (Bitboards.GetBishopAttacks(sq, occupied) & (board.Pieces[(int)PieceType.WhiteBishop] | board.Pieces[(int)PieceType.BlackBishop]
                     | board.Pieces[(int)PieceType.WhiteQueen] | board.Pieces[(int)PieceType.BlackQueen]))
                 | (Bitboards.GetRookAttacks(sq, occupied) & (board.Pieces[(int)PieceType.WhiteRook] | board.Pieces[(int)PieceType.BlackRook]
                     | board.Pieces[(int)PieceType.WhiteQueen] | board.Pieces[(int)PieceType.BlackQueen]))
                 | (Bitboards.KingAttacks[sq] & (board.Pieces[(int)PieceType.WhiteKing] | board.Pieces[(int)PieceType.BlackKing]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RevealXrayAttackers(Board board, int to, ulong occupied, ulong removedBB)
        {
            // After removing a piece, sliders behind it may now attack the target square.
            ulong newAttackers = 0;
            // Check diagonals (bishop + queen)
            ulong diagSliders = (board.Pieces[(int)PieceType.WhiteBishop] | board.Pieces[(int)PieceType.BlackBishop]
                               | board.Pieces[(int)PieceType.WhiteQueen] | board.Pieces[(int)PieceType.BlackQueen]) & occupied;
            if (diagSliders != 0)
                newAttackers |= Bitboards.GetBishopAttacks(to, occupied) & diagSliders;

            // Check straights (rook + queen)
            ulong straightSliders = (board.Pieces[(int)PieceType.WhiteRook] | board.Pieces[(int)PieceType.BlackRook]
                                   | board.Pieces[(int)PieceType.WhiteQueen] | board.Pieces[(int)PieceType.BlackQueen]) & occupied;
            if (straightSliders != 0)
                newAttackers |= Bitboards.GetRookAttacks(to, occupied) & straightSliders;

            return newAttackers;
        }

        // ──────────────────────────────────────────────────────────────
        //  Move Ordering
        // ──────────────────────────────────────────────────────────────

        private void ScoreMoves(Span<Move> moves, int count, Span<int> scores, Move ttMove, int ply, Board board, Move lastMove)
        {
            int sideIndex = board.WhiteToMove ? 0 : 1;
            Move counter = Move.None;
            if (lastMove != Move.None)
            {
                int prevPiece = (int)board.GetPieceAt(lastMove.To);
                if (prevPiece >= 0 && prevPiece < 12)
                    counter = _counterMoves[prevPiece, lastMove.To];
            }

            for (int i = 0; i < count; i++)
            {
                Move m = moves[i];

                if (m == ttMove)
                {
                    scores[i] = 10_000_000; // TT move first
                }
                else if (m.IsCapture)
                {
                    // MVV-LVA: victim value * 100 - attacker value + SEE bonus
                    int victim = m.Captured != PieceType.None ? PieceValues[(int)m.Captured] : 0;
                    PieceType attacker = board.GetPieceAt(m.From);
                    int attackerVal = attacker != PieceType.None ? PieceValues[(int)attacker] : 0;
                    int mvvLva = victim * 100 - attackerVal;

                    // Good captures above killers, bad captures below
                    if (SEE(board, m, 0))
                        scores[i] = 8_000_000 + mvvLva;
                    else
                        scores[i] = -2_000_000 + mvvLva; // losing capture
                }
                else if (m.IsPromotion)
                {
                    scores[i] = 9_000_000; // promotions right after TT
                }
                else if (m == _killers[ply * 2])
                {
                    scores[i] = 5_000_000;
                }
                else if (m == _killers[ply * 2 + 1])
                {
                    scores[i] = 4_000_000;
                }
                else if (counter != Move.None && m == counter)
                {
                    scores[i] = 3_500_000;
                }
                else
                {
                    // History heuristic
                    scores[i] = _history[sideIndex, m.From, m.To];
                }
            }
        }

        private static void ScoreQMoves(Span<Move> moves, int count, Span<int> scores, Board board)
        {
            for (int i = 0; i < count; i++)
            {
                Move m = moves[i];
                if (m.IsCapture)
                {
                    int victim = m.Captured != PieceType.None ? PieceValues[(int)m.Captured] : 0;
                    PieceType attacker = board.GetPieceAt(m.From);
                    int attackerVal = attacker != PieceType.None ? PieceValues[(int)attacker] : 0;
                    scores[i] = victim * 100 - attackerVal;
                }
                else if (m.IsPromotion)
                {
                    scores[i] = 9_000_000;
                }
                else
                {
                    scores[i] = 0; // check evasion quiet moves
                }
            }
        }

        /// <summary>
        /// Partial selection sort: swap the highest-scored move into position <paramref name="startIndex"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PickMove(Span<Move> moves, Span<int> scores, int startIndex, int count)
        {
            int bestIdx = startIndex;
            int bestScore = scores[startIndex];
            for (int j = startIndex + 1; j < count; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestScore = scores[j];
                    bestIdx = j;
                }
            }
            if (bestIdx != startIndex)
            {
                // Swap moves
                (moves[startIndex], moves[bestIdx]) = (moves[bestIdx], moves[startIndex]);
                (scores[startIndex], scores[bestIdx]) = (scores[bestIdx], scores[startIndex]);
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  PV helpers
        // ──────────────────────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePv(int ply, Move move)
        {
            int pvIdx = ply * MaxPly;
            _pvTable[pvIdx] = move;
            int childLen = (ply + 1 < MaxPly) ? _pvLength[ply + 1] : 0;
            int childStart = (ply + 1) * MaxPly;
            for (int i = 0; i < childLen; i++)
            {
                _pvTable[pvIdx + 1 + i] = _pvTable[childStart + i];
            }
            _pvLength[ply] = childLen + 1;
        }

        private string BuildPvString(int ply)
        {
            int len = _pvLength[ply];
            if (len == 0) return string.Empty;

            // Build on stack for short PVs to reduce allocations
            var sb = new System.Text.StringBuilder(len * 6);
            int pvIdx = ply * MaxPly;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(_pvTable[pvIdx + i].ToString());
            }
            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsRepetition(ulong hash, int ply)
        {
            for (int i = 0; i < _hashHistoryCount - 1; i++)
            {
                if (_hashHistory[i] == hash)
                    return true;
            }
            return false;
        }



        // ──────────────────────────────────────────────────────────────
        //  Utility
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether the side to move has at least one non-pawn, non-king piece.
        /// Used to gate null-move pruning (avoid zugzwang in pawn endings).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasNonPawnMaterial(Board board)
        {
            if (board.WhiteToMove)
            {
                return (board.Pieces[(int)PieceType.WhiteKnight]
                      | board.Pieces[(int)PieceType.WhiteBishop]
                      | board.Pieces[(int)PieceType.WhiteRook]
                      | board.Pieces[(int)PieceType.WhiteQueen]) != 0;
            }
            else
            {
                return (board.Pieces[(int)PieceType.BlackKnight]
                      | board.Pieces[(int)PieceType.BlackBishop]
                      | board.Pieces[(int)PieceType.BlackRook]
                      | board.Pieces[(int)PieceType.BlackQueen]) != 0;
            }
        }
    }
}
