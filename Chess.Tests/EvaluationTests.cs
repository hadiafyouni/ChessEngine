using Chess.Engine.Core;
using Chess.Engine.AI;

namespace Chess.Tests;

public class EvaluationTests
{
    // ── Starting Position Symmetry ─────────────────────────────────

    [Fact]
    public void StartingPosition_IsSymmetric()
    {
        var board = new Board();
        int score = Evaluation.Evaluate(board);

        // Starting position is perfectly symmetric, so the evaluation
        // from white's perspective should be ~0 (within ±5cp tolerance
        // to allow for tiny floating-point-like rounding in tapered eval).
        Assert.InRange(score, -5, 5);
    }

    // ── Mirrored Position Symmetry ─────────────────────────────────

    [Fact]
    public void MirroredPosition_Symmetric()
    {
        // A custom asymmetric FEN for white
        string whiteFen = "r1bqkbnr/pppppppp/2n5/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 1 2";
        // Its mirror: swap colors, flip ranks, swap turn
        // Original: black knight on c6 (sq 42), white pawn pushed to e4 (sq 28)
        // Mirror:   white knight on c3 (sq 18), black pawn pushed to e5 (sq 36)
        string blackFen = "rnbqkbnr/pppp1ppp/8/4p3/8/2N5/PPPPPPPP/R1BQKBNR b KQkq - 1 2";

        var whiteBoard = Board.FromFEN(whiteFen);
        var blackBoard = Board.FromFEN(blackFen);

        int whiteScore = Evaluation.Evaluate(whiteBoard); // from white-to-move perspective
        int blackScore = Evaluation.Evaluate(blackBoard); // from black-to-move perspective

        // Both evaluations are from the side-to-move's perspective, so
        // in a perfectly mirrored position they should be equal.
        Assert.InRange(Math.Abs(whiteScore - blackScore), 0, 5);
    }

    // ── Material Advantage ─────────────────────────────────────────

    [Fact]
    public void MaterialAdvantage_WhiteUp()
    {
        // White has an extra queen (queen on d1 + queen on d5), black has no queen
        // Position: normal-ish but white has a queen and black doesn't
        string fen = "rnb1kbnr/pppppppp/8/3Q4/8/8/PPPPPPPP/RNB1KBNR w KQkq - 0 1";
        var board = Board.FromFEN(fen);
        int score = Evaluation.Evaluate(board);

        // White is up a queen (900cp). Score from white's perspective should be > 800.
        Assert.True(score > 800, $"Expected score > 800 for white up a queen, got {score}");
    }

    [Fact]
    public void MaterialAdvantage_BlackUp()
    {
        // Black has an extra queen, white has no queen
        string fen = "rnb1kbnr/pppppppp/8/8/3q4/8/PPPPPPPP/RNB1KBNR b KQkq - 0 1";
        var board = Board.FromFEN(fen);
        int score = Evaluation.Evaluate(board);

        // Black to move, score is from black's perspective, should be > 800
        Assert.True(score > 800, $"Expected score > 800 for black up a queen, got {score}");
    }
}
