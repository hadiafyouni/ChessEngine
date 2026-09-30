using Chess.Engine.Core;
using Chess.Engine.AI;

namespace Chess.Tests;

public class TranspositionTableTests
{
    private static Move MakeDummyMove(int from, int to)
        => new Move(from, to, MoveFlag.Quiet);

    // ── Store and Probe: Exact Match ───────────────────────────────

    [Fact]
    public void StoreAndProbe_ExactMatch()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xDEADBEEF_12345678UL;
        int score = 150;
        int depth = 5;
        Move move = MakeDummyMove(12, 28); // e2e4
        int ply = 0;

        tt.Store(hash, score, depth, move, TTFlag.Exact, ply);

        int result = tt.Probe(hash, depth, -10000, 10000, ply, out Move probeMove);

        Assert.Equal(score, result);
        Assert.Equal(move, probeMove);
    }

    // ── Probe: Depth Insufficient ──────────────────────────────────

    [Fact]
    public void Probe_DepthInsufficient()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xCAFEBABE_CAFEBABUL;
        int score = 200;
        int storeDepth = 5;
        int probeDepth = 6; // deeper than stored
        Move move = MakeDummyMove(1, 18); // b1c3

        tt.Store(hash, score, storeDepth, move, TTFlag.Exact, 0);

        int result = tt.Probe(hash, probeDepth, -10000, 10000, 0, out Move probeMove);

        // Should miss on the score (depth insufficient), but still return best move
        Assert.Equal(TranspositionTable.TT_MISS, result);
        Assert.Equal(move, probeMove); // best move is still provided
    }

    // ── Store and Probe: Lower Bound ───────────────────────────────

    [Fact]
    public void StoreAndProbe_LowerBound()
    {
        var tt = new TranspositionTable();
        ulong hash = 0x1111_2222_3333_4444UL;
        int score = 300;  // stored as lower bound (fail-high)
        int depth = 4;
        Move move = MakeDummyMove(52, 36); // e7e5

        tt.Store(hash, score, depth, move, TTFlag.LowerBound, 0);

        // Probe with beta=250 (score 300 >= beta 250 → should return the score)
        int result = tt.Probe(hash, depth, -10000, 250, 0, out Move probeMove);

        Assert.Equal(score, result);
        Assert.Equal(move, probeMove);
    }

    // ── Store and Probe: Upper Bound ───────────────────────────────

    [Fact]
    public void StoreAndProbe_UpperBound()
    {
        var tt = new TranspositionTable();
        ulong hash = 0x5555_6666_7777_8888UL;
        int score = -200; // stored as upper bound (fail-low)
        int depth = 4;
        Move move = MakeDummyMove(6, 21); // g1f3

        tt.Store(hash, score, depth, move, TTFlag.UpperBound, 0);

        // Probe with alpha=-150 (score -200 <= alpha -150 → should return the score)
        int result = tt.Probe(hash, depth, -150, 10000, 0, out Move probeMove);

        Assert.Equal(score, result);
        Assert.Equal(move, probeMove);
    }

    // ── Probe: Complete Miss ───────────────────────────────────────

    [Fact]
    public void Probe_Miss()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xAAAA_BBBB_CCCC_DDDDUL;

        // Never stored anything for this hash
        int result = tt.Probe(hash, 1, -10000, 10000, 0, out Move probeMove);

        Assert.Equal(TranspositionTable.TT_MISS, result);
        Assert.Equal(Move.None, probeMove);
    }

    // ── Clear Removes Entries ──────────────────────────────────────

    [Fact]
    public void Clear_RemovesEntries()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xFEED_FACE_FEED_FACEUL;
        Move move = MakeDummyMove(4, 6); // e1g1 (king move)

        tt.Store(hash, 100, 5, move, TTFlag.Exact, 0);

        // Verify it's there
        int beforeClear = tt.Probe(hash, 5, -10000, 10000, 0, out _);
        Assert.Equal(100, beforeClear);

        // Clear and verify it's gone
        tt.Clear();
        int afterClear = tt.Probe(hash, 5, -10000, 10000, 0, out Move afterMove);

        Assert.Equal(TranspositionTable.TT_MISS, afterClear);
        Assert.Equal(Move.None, afterMove);
    }
}
