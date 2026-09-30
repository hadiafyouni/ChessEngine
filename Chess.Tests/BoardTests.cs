using Chess.Engine.Core;

namespace Chess.Tests;

public class BoardTests
{
    private const string StartingFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private const string KiwipeteFen =
        "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

    // ── FEN Round-Trip ─────────────────────────────────────────────

    [Fact]
    public void FenRoundTrip_StartingPosition()
    {
        var board = Board.FromFEN(StartingFen);
        string output = board.ToFEN();
        Assert.Equal(StartingFen, output);
    }

    [Fact]
    public void FenRoundTrip_Kiwipete()
    {
        var board = Board.FromFEN(KiwipeteFen);
        string output = board.ToFEN();
        Assert.Equal(KiwipeteFen, output);
    }

    // ── Make / Unmake Zobrist Integrity ─────────────────────────────

    [Fact]
    public void MakeUnmake_ZobristIntegrity()
    {
        var board = new Board();
        VerifyZobristRecursive(board, 3);
    }

    /// <summary>
    /// For every legal move at the current position, make the move, then unmake it,
    /// and verify the Zobrist hash and FEN return to the original state.
    /// Recurse up to <paramref name="depth"/> levels deep.
    /// </summary>
    private static void VerifyZobristRecursive(Board board, int depth)
    {
        if (depth == 0) return;

        string originalFen = board.ToFEN();
        ulong originalHash = board.ZobristHash;

        Span<Move> moves = stackalloc Move[256];
        int count = MoveGenerator.GenerateMoves(board, moves, false);

        for (int i = 0; i < count; i++)
        {
            Move m = moves[i];
            BoardState saved = board.MakeMove(m);

            // Recurse deeper
            VerifyZobristRecursive(board, depth - 1);

            board.UnmakeMove(m, saved);

            // After unmake the board must be identical to before the make
            Assert.Equal(originalHash, board.ZobristHash);
            Assert.Equal(originalFen, board.ToFEN());
        }
    }

    // ── 1000 Random Moves ──────────────────────────────────────────

    [Fact]
    public void MakeUnmake_1000RandomMoves()
    {
        var board = new Board();
        string initialFen = board.ToFEN();
        ulong initialHash = board.ZobristHash;

        var rng = new Random(42); // deterministic seed
        var playedMoves = new List<(Move move, BoardState state)>();

        Span<Move> moves = stackalloc Move[256];
        for (int i = 0; i < 1000; i++)
        {
            int count = MoveGenerator.GenerateMoves(board, moves, false);

            if (count == 0)
            {
                // Game over (checkmate or stalemate) – restart from initial
                // Unmake everything played so far
                for (int j = playedMoves.Count - 1; j >= 0; j--)
                {
                    board.UnmakeMove(playedMoves[j].move, playedMoves[j].state);
                }
                Assert.Equal(initialFen, board.ToFEN());
                Assert.Equal(initialHash, board.ZobristHash);
                playedMoves.Clear();

                // Reset the board to starting and continue
                board = new Board();
                continue;
            }

            int idx = rng.Next(count);
            Move m = moves[idx];
            BoardState saved = board.MakeMove(m);
            playedMoves.Add((m, saved));
        }

        // Unmake all remaining moves in reverse order
        for (int j = playedMoves.Count - 1; j >= 0; j--)
        {
            board.UnmakeMove(playedMoves[j].move, playedMoves[j].state);
        }

        Assert.Equal(initialFen, board.ToFEN());
        Assert.Equal(initialHash, board.ZobristHash);
    }

    // ── IsInCheck ──────────────────────────────────────────────────

    [Fact]
    public void TestMagicCollisions()
    {
        var sb = new System.Text.StringBuilder();
        // Check Bishop Magics
        for (int sq = 0; sq < 64; sq++)
        {
            ulong mask = Bitboards.BishopMasks[sq];
            int bits = GetBishopBits(sq);
            int size = 1 << bits;
            ulong[] expected = new ulong[size];
            bool[] used = new bool[size];
            ulong[] occSubsets = CreateSubsets(mask);
            
            int collisions = 0;
            foreach (var occ in occSubsets)
            {
                int index = (int)((occ * Bitboards.BishopMagics[sq]) >> (64 - bits));
                ulong attacks = Bitboards.GenerateBishopAttacks(sq, occ);
                if (used[index] && expected[index] != attacks)
                {
                    collisions++;
                }
                used[index] = true;
                expected[index] = attacks;
            }
            if (collisions > 0)
            {
                sb.AppendLine($"Bishop sq {sq} ({GetSquareName(sq)}) has {collisions} collisions! Magic used: 0x{Bitboards.BishopMagics[sq]:x}, bits: {bits}, mask popcount: {System.Numerics.BitOperations.PopCount(mask)}");
            }
        }

        // Check Rook Magics
        for (int sq = 0; sq < 64; sq++)
        {
            ulong mask = Bitboards.RookMasks[sq];
            int bits = GetRookBits(sq);
            int size = 1 << bits;
            ulong[] expected = new ulong[size];
            bool[] used = new bool[size];
            ulong[] occSubsets = CreateSubsets(mask);
            
            int collisions = 0;
            foreach (var occ in occSubsets)
            {
                int index = (int)((occ * Bitboards.RookMagics[sq]) >> (64 - bits));
                ulong attacks = Bitboards.GenerateRookAttacks(sq, occ);
                if (used[index] && expected[index] != attacks)
                {
                    collisions++;
                }
                used[index] = true;
                expected[index] = attacks;
            }
            if (collisions > 0)
            {
                sb.AppendLine($"Rook sq {sq} ({GetSquareName(sq)}) has {collisions} collisions! Magic used: 0x{Bitboards.RookMagics[sq]:x}, bits: {bits}, mask popcount: {System.Numerics.BitOperations.PopCount(mask)}");
            }
        }
        Assert.True(sb.Length == 0, sb.ToString());
    }

    private static int GetRookBits(int sq)
    {
        int[] RookBits = new int[64] {
            12, 11, 11, 11, 11, 11, 11, 12,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            12, 11, 11, 11, 11, 11, 11, 12
        };
        return RookBits[sq];
    }

    private static int GetBishopBits(int sq)
    {
        int[] BishopBits = new int[64] {
            6, 5, 5, 5, 5, 5, 5, 6,
            5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 7, 7, 7, 7, 5, 5,
            5, 5, 7, 9, 9, 7, 5, 5,
            5, 5, 7, 9, 9, 7, 5, 5,
            5, 5, 7, 7, 7, 7, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5,
            6, 5, 5, 5, 5, 5, 5, 6
        };
        return BishopBits[sq];
    }

    private static ulong[] CreateSubsets(ulong mask)
    {
        var list = new System.Collections.Generic.List<ulong>();
        int[] indices = new int[64];
        int count = 0;
        ulong temp = mask;
        while (temp != 0)
        {
            indices[count++] = System.Numerics.BitOperations.TrailingZeroCount(temp);
            temp &= temp - 1;
        }
        int num = 1 << count;
        ulong[] result = new ulong[num];
        for (int i = 0; i < num; i++)
        {
            ulong subset = 0;
            for (int j = 0; j < count; j++)
            {
                if ((i & (1 << j)) != 0)
                {
                    subset |= 1UL << indices[j];
                }
            }
            result[i] = subset;
        }
        return result;
    }

    private static string GetMoveString(Move m)
    {
        int from = m.From;
        int to = m.To;
        string fromName = GetSquareName(from);
        string toName = GetSquareName(to);
        string promo = m.Promotion switch
        {
            PieceType.WhiteKnight or PieceType.BlackKnight => "n",
            PieceType.WhiteBishop or PieceType.BlackBishop => "b",
            PieceType.WhiteRook or PieceType.BlackRook => "r",
            PieceType.WhiteQueen or PieceType.BlackQueen => "q",
            _ => ""
        };
        return $"{fromName}{toName}{promo}";
    }

    private static string GetSquareName(int sq)
    {
        int file = sq % 8;
        int rank = sq / 8;
        return $"{(char)('a' + file)}{rank + 1}";
    }

    [Fact]
    public void IsInCheck_StartingPosition()
    {
        var board = new Board();
        Assert.False(board.IsInCheck(true),  "White should not be in check at start");
        Assert.False(board.IsInCheck(false), "Black should not be in check at start");
    }

    [Fact]
    public void IsInCheck_ScholarsMate()
    {
        // After 1.e4 e5 2.Qh5 Nc6 3.Bc4 Nf6 4.Qxf7# – Black king is checkmated
        var board = Board.FromFEN("r1bqkb1r/pppp1Qpp/2n2n2/4p3/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 0 4");
        Assert.True(board.IsInCheck(false), "Black king should be in check (Scholar's mate)");
        Assert.False(board.IsInCheck(true), "White king should not be in check");
    }
}


