using Chess.Engine.Core;

namespace Chess.Tests;

public class PerftTests
{
    /// <summary>
    /// Recursively counts all leaf nodes at the given depth.
    /// Uses a local stackalloc buffer to avoid clobbering the ThreadStatic pool
    /// used by MoveGenerator.GenerateMoves(Board).
    /// </summary>
    private static long Perft(Board board, int depth)
    {
        if (depth == 0)
            return 1;

        Span<Move> moves = stackalloc Move[256];
        int count = MoveGenerator.GenerateMoves(board, moves, false);

        long nodes = 0;
        for (int i = 0; i < count; i++)
        {
            Move m = moves[i];
            BoardState saved = board.MakeMove(m);
            nodes += Perft(board, depth - 1);
            board.UnmakeMove(m, saved);
        }

        return nodes;
    }

    // ── Starting Position ──────────────────────────────────────────

    [Fact]
    public void StartingPosition_Depth1()
    {
        var board = new Board();
        Assert.Equal(20, Perft(board, 1));
    }

    [Fact]
    public void StartingPosition_Depth2()
    {
        var board = new Board();
        Assert.Equal(400, Perft(board, 2));
    }

    [Fact]
    public void StartingPosition_Depth3()
    {
        var board = new Board();
        Assert.Equal(8902, Perft(board, 3));
    }

    [Fact]
    public void StartingPosition_Depth4()
    {
        var board = new Board();
        Assert.Equal(197281, Perft(board, 4));
    }

    [Fact]
    public void StartingPosition_Depth5()
    {
        var board = new Board();
        Assert.Equal(4865609, Perft(board, 5));
    }

    // ── Kiwipete Position ──────────────────────────────────────────

    private const string KiwipeteFen =
        "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

    [Fact]
    public void Kiwipete_Depth1()
    {
        var board = Board.FromFEN(KiwipeteFen);
        Assert.Equal(48, Perft(board, 1));
    }

    [Fact]
    public void Kiwipete_Depth2()
    {
        var board = Board.FromFEN(KiwipeteFen);
        Assert.Equal(2039, Perft(board, 2));
    }

    [Fact]
    public void Kiwipete_Depth3()
    {
        var board = Board.FromFEN(KiwipeteFen);
        Assert.Equal(97862, Perft(board, 3));
    }

    [Fact]
    public void Kiwipete_Depth4()
    {
        var board = Board.FromFEN(KiwipeteFen);
        Assert.Equal(4085603, Perft(board, 4));
    }
}
