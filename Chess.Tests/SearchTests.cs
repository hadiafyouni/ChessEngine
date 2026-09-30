using Chess.Engine.Core;
using Chess.Engine.AI;

namespace Chess.Tests;

/// <summary>
/// Search tests that validate the engine finds obvious best moves.
/// These tests use a simple iterative-deepening alpha-beta search
/// built directly on the existing MoveGenerator, Evaluation, and
/// TranspositionTable so they work even if a standalone Search class
/// hasn't been created yet.
/// </summary>
public class SearchTests
{
    // ── Lightweight search harness ──────────────────────────────────

    /// <summary>
    /// Simple negamax alpha-beta search to find the best move.
    /// </summary>
    private static (Move bestMove, int score) FindBestMove(Board board, int maxDepth)
    {
        Move bestMove = Move.None;
        int bestScore = int.MinValue + 1;

        Span<Move> moves = stackalloc Move[256];
        int count = MoveGenerator.GenerateMoves(board, moves, false);

        for (int i = 0; i < count; i++)
        {
            Move m = moves[i];
            BoardState saved = board.MakeMove(m);
            int score = -NegaMax(board, maxDepth - 1, -100000, 100000);
            board.UnmakeMove(m, saved);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = m;
            }
        }

        return (bestMove, bestScore);
    }

    private static int NegaMax(Board board, int depth, int alpha, int beta)
    {
        Span<Move> moves = stackalloc Move[256];
        int count = MoveGenerator.GenerateMoves(board, moves, false);

        if (count == 0)
        {
            // No legal moves: checkmate or stalemate
            if (board.IsInCheck(board.WhiteToMove))
                return -50000 - depth; // Checkmate (worse the deeper we are = prefer faster mates)
            return 0; // Stalemate
        }

        if (depth == 0)
            return Evaluation.Evaluate(board);

        for (int i = 0; i < count; i++)
        {
            Move m = moves[i];
            BoardState saved = board.MakeMove(m);
            int score = -NegaMax(board, depth - 1, -beta, -alpha);
            board.UnmakeMove(m, saved);

            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    /// <summary>
    /// Check whether the given move results in checkmate (opponent has no legal moves
    /// and is in check).
    /// </summary>
    private static bool IsCheckmate(Board board, Move move)
    {
        BoardState saved = board.MakeMove(move);
        Span<Move> responses = stackalloc Move[256];
        int responseCount = MoveGenerator.GenerateMoves(board, responses, false);
        bool isMate = responseCount == 0 && board.IsInCheck(board.WhiteToMove);
        board.UnmakeMove(move, saved);
        return isMate;
    }

    // ── Mate in 1: White ───────────────────────────────────────────

    [Fact]
    public void MateIn1_White()
    {
        // White rook on a1, white king on e1. Black king on h8, black pawns f7 g7 h7.
        // Ra1-a8# is mate in 1.
        string fen = "7k/5ppp/8/8/8/8/8/R3K3 w Q - 0 1";
        var board = Board.FromFEN(fen);

        var (bestMove, _) = FindBestMove(board, 3);

        Assert.True(bestMove != Move.None, "Search should find a move");
        Assert.True(IsCheckmate(board, bestMove),
            $"Expected a checkmating move, but got {bestMove}");
    }

    // ── Mate in 1: Black ───────────────────────────────────────────

    [Fact]
    public void MateIn1_Black()
    {
        // Mirror of the white case: Black rook on a8, black king on e8.
        // White king on h1, white pawns f2 g2 h2. Ra8-a1# is mate.
        string fen = "r3k3/8/8/8/8/8/5PPP/7K b q - 0 1";
        var board = Board.FromFEN(fen);

        var (bestMove, _) = FindBestMove(board, 3);

        Assert.True(bestMove != Move.None, "Search should find a move");
        Assert.True(IsCheckmate(board, bestMove),
            $"Expected a checkmating move, but got {bestMove}");
    }

    // ── Winning Capture ────────────────────────────────────────────

    [Fact]
    public void WinningCapture()
    {
        // White knight on e4, black queen hanging on d6, kings on opposite sides.
        // White should capture the queen.
        string fen = "4k3/8/3q4/8/4N3/8/8/4K3 w - - 0 1";
        var board = Board.FromFEN(fen);

        var (bestMove, _) = FindBestMove(board, 2);

        Assert.True(bestMove != Move.None, "Search should find a move");
        Assert.True(bestMove.IsCapture,
            $"Expected a capture, but got {bestMove} (flag={bestMove.Flag})");
        Assert.Equal(PieceType.BlackQueen, bestMove.Captured);
    }

    // ── Avoid Stalemate ────────────────────────────────────────────

    [Fact]
    public void AvoidStalemate()
    {
        // White queen on g6, white king on f8, black king on h8.
        // Qg7 would be stalemate! White should avoid it (e.g. Qf7, Qe8+, etc.)
        string fen = "7k/8/6Q1/8/8/8/8/5K2 w - - 0 1";
        var board = Board.FromFEN(fen);

        var (bestMove, score) = FindBestMove(board, 3);

        Assert.True(bestMove != Move.None, "Search should find a move");

        // After the best move, the opponent should still have legal moves
        // (i.e. it's not stalemate) OR it should be checkmate.
        BoardState saved = board.MakeMove(bestMove);
        Span<Move> responses = stackalloc Move[256];
        int responseCount = MoveGenerator.GenerateMoves(board, responses, false);

        bool isCheckmate = responseCount == 0 && board.IsInCheck(board.WhiteToMove);
        bool isStalemate = responseCount == 0 && !board.IsInCheck(board.WhiteToMove);

        board.UnmakeMove(bestMove, saved);

        Assert.False(isStalemate,
            $"Engine played {bestMove} which causes stalemate – should have been avoided");
    }

    // ── Search Returns Valid Move ──────────────────────────────────

    [Fact]
    public void Search_ReturnsValidMove()
    {
        var board = new Board();
        var (bestMove, _) = FindBestMove(board, 4);

        Assert.True(bestMove != Move.None, "Search should return a move");

        // Verify the returned move is actually in the legal move list
        Span<Move> legalMoves = stackalloc Move[256];
        int count = MoveGenerator.GenerateMoves(board, legalMoves, false);

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (legalMoves[i] == bestMove)
            {
                found = true;
                break;
            }
        }

        Assert.True(found, $"Best move {bestMove} is not in the legal move list");
    }
}
