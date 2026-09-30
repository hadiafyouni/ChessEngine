using System;
using System.Numerics;
using Chess.Engine.Core;

namespace Chess.Engine.AI
{
    public static class Evaluation
    {
        // Piece values
        public const int PawnValue = 100;
        public const int KnightValue = 320;
        public const int BishopValue = 330;
        public const int RookValue = 500;
        public const int QueenValue = 900;
        public const int KingValue = 20000;

        // File masks and passed pawn masks
        public static readonly ulong[] FileMasks = new ulong[8];
        public static readonly ulong[,] PassedPawnMasks = new ulong[2, 64];

        // Piece-Square Tables (Middlegame and Endgame)
        // Values from Chess Programming Wiki (CPW) / Fruit engine
        
        private static readonly int[] MgPawnPst = {
              0,   0,   0,   0,   0,   0,   0,   0,
             50,  50,  50,  50,  50,  50,  50,  50,
             10,  10,  20,  30,  30,  20,  10,  10,
              5,   5,  10,  25,  25,  10,   5,   5,
              0,   0,   0,  20,  20,   0,   0,   0,
              5,  -5, -10,   0,   0, -10,  -5,   5,
              5,  10,  10, -20, -20,  10,  10,   5,
              0,   0,   0,   0,   0,   0,   0,   0
        };

        private static readonly int[] EgPawnPst = {
              0,   0,   0,   0,   0,   0,   0,   0,
             50,  50,  50,  50,  50,  50,  50,  50,
             30,  30,  30,  30,  30,  30,  30,  30,
             20,  20,  20,  20,  20,  20,  20,  20,
             10,  10,  10,  10,  10,  10,  10,  10,
              5,   5,   5,   5,   5,   5,   5,   5,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0
        };

        private static readonly int[] MgKnightPst = {
            -50, -40, -30, -30, -30, -30, -40, -50,
            -40, -20,   0,   0,   0,   0, -20, -40,
            -30,   0,  10,  15,  15,  10,   0, -30,
            -30,   5,  15,  20,  20,  15,   5, -30,
            -30,   0,  15,  20,  20,  15,   0, -30,
            -30,   5,  10,  15,  15,  10,   5, -30,
            -40, -20,   0,   5,   5,   0, -20, -40,
            -50, -40, -30, -30, -30, -30, -40, -50
        };

        private static readonly int[] EgKnightPst = {
            -50, -40, -30, -30, -30, -30, -40, -50,
            -40, -20,   0,   5,   5,   0, -20, -40,
            -30,   0,  10,  15,  15,  10,   0, -30,
            -30,   5,  15,  20,  20,  15,   5, -30,
            -30,   0,  15,  20,  20,  15,   0, -30,
            -30,   5,  10,  15,  15,  10,   5, -30,
            -40, -20,   0,   5,   5,   0, -20, -40,
            -50, -40, -30, -30, -30, -30, -40, -50
        };

        private static readonly int[] MgBishopPst = {
            -20, -10, -10, -10, -10, -10, -10, -20,
            -10,   0,   0,   0,   0,   0,   0, -10,
            -10,   0,   5,  10,  10,   5,   0, -10,
            -10,   5,   5,  10,  10,   5,   5, -10,
            -10,   0,  10,  10,  10,  10,   0, -10,
            -10,  10,  10,  10,  10,  10,  10, -10,
            -10,   5,   0,   0,   0,   0,   5, -10,
            -20, -10, -10, -10, -10, -10, -10, -20
        };

        private static readonly int[] EgBishopPst = {
            -20, -10, -10, -10, -10, -10, -10, -20,
            -10,   0,   0,   0,   0,   0,   0, -10,
            -10,   0,   5,  10,  10,   5,   0, -10,
            -10,   5,   5,  10,  10,   5,   5, -10,
            -10,   0,  10,  10,  10,  10,   0, -10,
            -10,  10,  10,  10,  10,  10,  10, -10,
            -10,   5,   0,   0,   0,   0,   5, -10,
            -20, -10, -10, -10, -10, -10, -10, -20
        };

        private static readonly int[] MgRookPst = {
              0,   0,   0,   0,   0,   0,   0,   0,
              5,  10,  10,  10,  10,  10,  10,   5,
             -5,   0,   0,   0,   0,   0,   0,  -5,
             -5,   0,   0,   0,   0,   0,   0,  -5,
             -5,   0,   0,   0,   0,   0,   0,  -5,
             -5,   0,   0,   0,   0,   0,   0,  -5,
             -5,   0,   0,   0,   0,   0,   0,  -5,
              0,   0,   0,   5,   5,   0,   0,   0
        };

        private static readonly int[] EgRookPst = {
              0,   0,   0,   0,   0,   0,   0,   0,
              5,  10,  10,  10,  10,  10,  10,   5,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0
        };

        private static readonly int[] MgQueenPst = {
            -20, -10, -10,  -5,  -5, -10, -10, -20,
            -10,   0,   0,   0,   0,   0,   0, -10,
            -10,   0,   5,   5,   5,   5,   0, -10,
             -5,   0,   5,   5,   5,   5,   0,  -5,
              0,   0,   5,   5,   5,   5,   0,  -5,
            -10,   5,   5,   5,   5,   5,   0, -10,
            -10,   0,   5,   0,   0,   0,   0, -10,
            -20, -10, -10,  -5,  -5, -10, -10, -20
        };

        private static readonly int[] EgQueenPst = {
            -20, -10, -10,  -5,  -5, -10, -10, -20,
            -10,   0,   5,   5,   5,   5,   0, -10,
            -10,   5,   5,   5,   5,   5,   5, -10,
             -5,   5,   5,   5,   5,   5,   5,  -5,
              0,   5,   5,   5,   5,   5,   5,  -5,
            -10,   5,   5,   5,   5,   5,   5, -10,
            -10,   0,   5,   5,   5,   5,   0, -10,
            -20, -10, -10,  -5,  -5, -10, -10, -20
        };

        private static readonly int[] MgKingPst = {
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -20, -30, -30, -40, -40, -30, -30, -20,
            -10, -20, -20, -20, -20, -20, -20, -10,
             20,  20,   0,   0,   0,   0,  20,  20,
             20,  30,  10,   0,   0,  10,  30,  20
        };

        private static readonly int[] EgKingPst = {
            -50, -40, -30, -20, -20, -30, -40, -50,
            -30, -20, -10,   0,   0, -10, -20, -30,
            -30, -10,  20,  30,  30,  20, -10, -30,
            -30, -10,  30,  40,  40,  30, -10, -30,
            -30, -10,  30,  40,  40,  30, -10, -30,
            -30, -10,  20,  30,  30,  20, -10, -30,
            -30, -30,   0,   0,   0,   0, -30, -30,
            -50, -30, -30, -30, -30, -30, -30, -50
        };

        static Evaluation()
        {
            // Set up FileMasks
            for (int f = 0; f < 8; f++)
            {
                ulong mask = 0;
                for (int r = 0; r < 8; r++)
                {
                    mask |= 1UL << (r * 8 + f);
                }
                FileMasks[f] = mask;
            }

            // Set up PassedPawnMasks
            for (int sq = 0; sq < 64; sq++)
            {
                int r = sq / 8;
                int f = sq % 8;

                // White passed pawn mask
                ulong wMask = 0;
                for (int file = Math.Max(0, f - 1); file <= Math.Min(7, f + 1); file++)
                {
                    for (int rank = r + 1; rank < 8; rank++)
                    {
                        wMask |= 1UL << (rank * 8 + file);
                    }
                }
                PassedPawnMasks[0, sq] = wMask;

                // Black passed pawn mask
                ulong bMask = 0;
                for (int file = Math.Max(0, f - 1); file <= Math.Min(7, f + 1); file++)
                {
                    for (int rank = 0; rank < r; rank++)
                    {
                        bMask |= 1UL << (rank * 8 + file);
                    }
                }
                PassedPawnMasks[1, sq] = bMask;
            }
        }

        public static int Evaluate(Board board)
        {
            // Evaluate White material and PST
            int whiteMg = 0;
            int whiteEg = 0;
            
            // Evaluate Black material and PST
            int blackMg = 0;
            int blackEg = 0;

            // Non-pawn phase calculation
            int phase = 0;

            for (int p = 0; p < 12; p++)
            {
                ulong bb = board.Pieces[p];
                int count = Bitboards.PopCount(bb);

                // Add material values and calculate phase
                switch ((PieceType)p)
                {
                    case PieceType.WhitePawn:
                        whiteMg += count * PawnValue;
                        whiteEg += count * PawnValue;
                        break;
                    case PieceType.WhiteKnight:
                        whiteMg += count * KnightValue;
                        whiteEg += count * KnightValue;
                        phase += count * 1;
                        break;
                    case PieceType.WhiteBishop:
                        whiteMg += count * BishopValue;
                        whiteEg += count * BishopValue;
                        phase += count * 1;
                        break;
                    case PieceType.WhiteRook:
                        whiteMg += count * RookValue;
                        whiteEg += count * RookValue;
                        phase += count * 2;
                        break;
                    case PieceType.WhiteQueen:
                        whiteMg += count * QueenValue;
                        whiteEg += count * QueenValue;
                        phase += count * 4;
                        break;
                    case PieceType.WhiteKing:
                        whiteMg += count * KingValue;
                        whiteEg += count * KingValue;
                        break;

                    case PieceType.BlackPawn:
                        blackMg += count * PawnValue;
                        blackEg += count * PawnValue;
                        break;
                    case PieceType.BlackKnight:
                        blackMg += count * KnightValue;
                        blackEg += count * KnightValue;
                        phase += count * 1;
                        break;
                    case PieceType.BlackBishop:
                        blackMg += count * BishopValue;
                        blackEg += count * BishopValue;
                        phase += count * 1;
                        break;
                    case PieceType.BlackRook:
                        blackMg += count * RookValue;
                        blackEg += count * RookValue;
                        phase += count * 2;
                        break;
                    case PieceType.BlackQueen:
                        blackMg += count * QueenValue;
                        blackEg += count * QueenValue;
                        phase += count * 4;
                        break;
                    case PieceType.BlackKing:
                        blackMg += count * KingValue;
                        blackEg += count * KingValue;
                        break;
                }

                // Add PST values
                while (bb != 0)
                {
                    int sq = Bitboards.PopLSB(ref bb);
                    int whiteSq = sq;
                    int blackSq = sq ^ 56; // Mirrored vertically for black

                    switch ((PieceType)p)
                    {
                        case PieceType.WhitePawn:
                            whiteMg += MgPawnPst[whiteSq];
                            whiteEg += EgPawnPst[whiteSq];
                            break;
                        case PieceType.WhiteKnight:
                            whiteMg += MgKnightPst[whiteSq];
                            whiteEg += EgKnightPst[whiteSq];
                            break;
                        case PieceType.WhiteBishop:
                            whiteMg += MgBishopPst[whiteSq];
                            whiteEg += EgBishopPst[whiteSq];
                            break;
                        case PieceType.WhiteRook:
                            whiteMg += MgRookPst[whiteSq];
                            whiteEg += EgRookPst[whiteSq];
                            break;
                        case PieceType.WhiteQueen:
                            whiteMg += MgQueenPst[whiteSq];
                            whiteEg += EgQueenPst[whiteSq];
                            break;
                        case PieceType.WhiteKing:
                            whiteMg += MgKingPst[whiteSq];
                            whiteEg += EgKingPst[whiteSq];
                            break;

                        case PieceType.BlackPawn:
                            blackMg += MgPawnPst[blackSq];
                            blackEg += EgPawnPst[blackSq];
                            break;
                        case PieceType.BlackKnight:
                            blackMg += MgKnightPst[blackSq];
                            blackEg += EgKnightPst[blackSq];
                            break;
                        case PieceType.BlackBishop:
                            blackMg += MgBishopPst[blackSq];
                            blackEg += EgBishopPst[blackSq];
                            break;
                        case PieceType.BlackRook:
                            blackMg += MgRookPst[blackSq];
                            blackEg += EgRookPst[blackSq];
                            break;
                        case PieceType.BlackQueen:
                            blackMg += MgQueenPst[blackSq];
                            blackEg += EgQueenPst[blackSq];
                            break;
                        case PieceType.BlackKing:
                            blackMg += MgKingPst[blackSq];
                            blackEg += EgKingPst[blackSq];
                            break;
                    }
                }
            }

            // Cap phase at 24
            phase = Math.Min(phase, 24);

            // Bishop pair: both bishops → bonus (more valuable in EG with open board)
            int whiteBishops = Bitboards.PopCount(board.Pieces[(int)PieceType.WhiteBishop]);
            int blackBishops = Bitboards.PopCount(board.Pieces[(int)PieceType.BlackBishop]);
            if (whiteBishops >= 2) { whiteMg += 25; whiteEg += 50; }
            if (blackBishops >= 2) { blackMg += 25; blackEg += 50; }

            // Add Pawn Structure
            var (whitePawnMg, whitePawnEg) = EvaluatePawnStructure(board, true);
            var (blackPawnMg, blackPawnEg) = EvaluatePawnStructure(board, false);
            whiteMg += whitePawnMg;
            whiteEg += whitePawnEg;
            blackMg += blackPawnMg;
            blackEg += blackPawnEg;

            // Add Mobility
            var (whiteMobilityMg, whiteMobilityEg) = EvaluateMobility(board, true);
            var (blackMobilityMg, blackMobilityEg) = EvaluateMobility(board, false);
            whiteMg += whiteMobilityMg;
            whiteEg += whiteMobilityEg;
            blackMg += blackMobilityMg;
            blackEg += blackMobilityEg;

            // Add King Safety
            var (whiteKingMg, whiteKingEg) = EvaluateKingSafety(board, true);
            var (blackKingMg, blackKingEg) = EvaluateKingSafety(board, false);
            whiteMg += whiteKingMg;
            whiteEg += whiteKingEg;
            blackMg += blackKingMg;
            blackEg += blackKingEg;

            // Add Rooks
            var (whiteRookMg, whiteRookEg) = EvaluateRooks(board, true);
            var (blackRookMg, blackRookEg) = EvaluateRooks(board, false);
            whiteMg += whiteRookMg;
            whiteEg += whiteRookEg;
            blackMg += blackRookMg;
            blackEg += blackRookEg;

            // Tapered Evaluation Blending
            int scoreMg = whiteMg - blackMg;
            int scoreEg = whiteEg - blackEg;
            int score = (scoreMg * phase + scoreEg * (24 - phase)) / 24;

            // Negate from perspective of side to move
            return board.WhiteToMove ? score : -score;
        }

        private static (int mg, int eg) EvaluatePawnStructure(Board board, bool isWhite)
        {
            int mg = 0;
            int eg = 0;
            int offset = isWhite ? 0 : 6;
            ulong pawns = board.Pieces[offset + 0];
            ulong oppPawns = board.Pieces[(isWhite ? 6 : 0) + 0];

            // Doubled Pawns
            for (int f = 0; f < 8; f++)
            {
                ulong fileMask = FileMasks[f];
                int count = Bitboards.PopCount(pawns & fileMask);
                if (count > 1)
                {
                    int penalty = (count - 1) * 20;
                    mg -= penalty;
                    eg -= penalty;
                }
            }

            // Isolated and Passed Pawns
            ulong tempPawns = pawns;
            while (tempPawns != 0)
            {
                int sq = Bitboards.PopLSB(ref tempPawns);
                int file = sq % 8;
                int rank = sq / 8;

                // Isolated Pawns
                ulong adjFilesMask = 0;
                if (file > 0) adjFilesMask |= FileMasks[file - 1];
                if (file < 7) adjFilesMask |= FileMasks[file + 1];
                if ((pawns & adjFilesMask) == 0)
                {
                    mg -= 15;
                    eg -= 15;
                }

                // Backward pawn: pawn cannot advance without being captured, and no support
                ulong stopSquare = isWhite ? (1UL << (sq + 8)) : (1UL << (sq - 8));
                ulong oppPawnAttacks = isWhite
                    ? (((oppPawns & ~FileMasks[0]) >> 9) | ((oppPawns & ~FileMasks[7]) >> 7))
                    : (((oppPawns & ~FileMasks[0]) << 7) | ((oppPawns & ~FileMasks[7]) << 9));
                bool stopAttackedByOpp = (stopSquare & oppPawnAttacks) != 0;
                bool supportedByFriendly = (adjFilesMask != 0) && ((pawns & adjFilesMask) != 0);
                if (stopAttackedByOpp && !supportedByFriendly)
                {
                    mg -= 12;
                    eg -= 8;
                }

                // Passed Pawns
                ulong passedMask = PassedPawnMasks[isWhite ? 0 : 1, sq];
                if ((passedMask & oppPawns) == 0)
                {
                    int relativeRank = isWhite ? rank : 7 - rank;
                    int bonus = 30 + (relativeRank - 1) * 10;
                    mg += bonus;
                    eg += (int)(bonus * 1.5);
                }
            }

            return (mg, eg);
        }

        private static (int mg, int eg) EvaluateMobility(Board board, bool isWhite)
        {
            int mg = 0;
            int eg = 0;
            int offset = isWhite ? 0 : 6;
            ulong occupied = board.Occupied;
            ulong friendlyOccupied = isWhite ? board.WhiteOccupied : board.BlackOccupied;

            // Knights
            ulong knights = board.Pieces[offset + 1];
            while (knights != 0)
            {
                int sq = Bitboards.PopLSB(ref knights);
                ulong targets = Bitboards.KnightAttacks[sq] & ~friendlyOccupied;
                int count = Bitboards.PopCount(targets);
                mg += count * 3;
                eg += count * 2;
            }

            // Bishops
            ulong bishops = board.Pieces[offset + 2];
            while (bishops != 0)
            {
                int sq = Bitboards.PopLSB(ref bishops);
                ulong targets = Bitboards.GetBishopAttacks(sq, occupied) & ~friendlyOccupied;
                int count = Bitboards.PopCount(targets);
                mg += count * 3;
                eg += count * 2;
            }

            // Rooks
            ulong rooks = board.Pieces[offset + 3];
            while (rooks != 0)
            {
                int sq = Bitboards.PopLSB(ref rooks);
                ulong targets = Bitboards.GetRookAttacks(sq, occupied) & ~friendlyOccupied;
                int count = Bitboards.PopCount(targets);
                mg += count * 3;
                eg += count * 2;
            }

            // Queens
            ulong queens = board.Pieces[offset + 4];
            while (queens != 0)
            {
                int sq = Bitboards.PopLSB(ref queens);
                ulong targets = Bitboards.GetQueenAttacks(sq, occupied) & ~friendlyOccupied;
                int count = Bitboards.PopCount(targets);
                mg += count * 3;
                eg += count * 2;
            }

            return (mg, eg);
        }

        private static (int mg, int eg) EvaluateKingSafety(Board board, bool isWhite)
        {
            int score = 0;
            int offset = isWhite ? 0 : 6;
            int kingSq = Bitboards.LSB(board.Pieces[offset + 5]);
            if (kingSq >= 64) return (0, 0);

            int kFile = kingSq % 8;
            int kRank = kingSq / 8;
            ulong pawns = board.Pieces[offset + 0];
            ulong oppOccupied = isWhite ? board.BlackOccupied : board.WhiteOccupied;

            // 1. Pawn Shelter (only evaluate if king on back ranks)
            bool evaluateShelter = isWhite ? kRank <= 2 : kRank >= 5;
            if (evaluateShelter)
            {
                // Shelter files to check: kFile-1, kFile, kFile+1
                for (int f = Math.Max(0, kFile - 1); f <= Math.Min(7, kFile + 1); f++)
                {
                    ulong fileMask = FileMasks[f];
                    ulong shelterPawns = pawns & fileMask;
                    
                    // We expect a friendly pawn on these files in front of king
                    bool hasPawn = false;
                    if (isWhite)
                    {
                        // Check ranks 1 and 2
                        hasPawn = (shelterPawns & ((1UL << (8 + f)) | (1UL << (16 + f)))) != 0;
                    }
                    else
                    {
                        // Check ranks 6 and 5
                        hasPawn = (shelterPawns & ((1UL << (48 + f)) | (1UL << (40 + f)))) != 0;
                    }

                    if (!hasPawn)
                    {
                        score -= 15; // Missing pawn shelter penalty
                    }
                }
            }

            // 2. Open / Semi-open files near King
            for (int f = Math.Max(0, kFile - 1); f <= Math.Min(7, kFile + 1); f++)
            {
                ulong fileMask = FileMasks[f];
                if ((pawns & fileMask) == 0)
                {
                    score -= 15; // Semi-open file near king penalty
                    if ((board.Pieces[(isWhite ? 6 : 0) + 0] & fileMask) == 0)
                    {
                        score -= 10; // Open file near king penalty (both sides empty)
                    }
                }
            }

            // 3. Opponent Attackers near King
            // Count opponent pieces attacking squares adjacent to the king
            ulong adjacentSquares = Bitboards.KingAttacks[kingSq];
            int oppOffset = isWhite ? 6 : 0;
            int attackerCount = 0;

            // Check Knights
            ulong oppKnights = board.Pieces[oppOffset + 1];
            while (oppKnights != 0)
            {
                int sq = Bitboards.PopLSB(ref oppKnights);
                if ((Bitboards.KnightAttacks[sq] & (adjacentSquares | (1UL << kingSq))) != 0) attackerCount++;
            }

            // Check Bishops
            ulong oppBishops = board.Pieces[oppOffset + 2];
            while (oppBishops != 0)
            {
                int sq = Bitboards.PopLSB(ref oppBishops);
                if ((Bitboards.GetBishopAttacks(sq, board.Occupied) & (adjacentSquares | (1UL << kingSq))) != 0) attackerCount++;
            }

            // Check Rooks
            ulong oppRooks = board.Pieces[oppOffset + 3];
            while (oppRooks != 0)
            {
                int sq = Bitboards.PopLSB(ref oppRooks);
                if ((Bitboards.GetRookAttacks(sq, board.Occupied) & (adjacentSquares | (1UL << kingSq))) != 0) attackerCount++;
            }

            // Check Queens
            ulong oppQueens = board.Pieces[oppOffset + 4];
            while (oppQueens != 0)
            {
                int sq = Bitboards.PopLSB(ref oppQueens);
                if ((Bitboards.GetQueenAttacks(sq, board.Occupied) & (adjacentSquares | (1UL << kingSq))) != 0) attackerCount++;
            }

            if (attackerCount > 0)
            {
                // Attack scale factor penalty: -10cp for 1 attacker, -25cp for 2, -45cp for 3, etc.
                score -= attackerCount * attackerCount * 5;
            }

            return (score, score / 4);
        }

        private static (int mg, int eg) EvaluateRooks(Board board, bool isWhite)
        {
            int mg = 0, eg = 0;
            int offset = isWhite ? 0 : 6;
            ulong rooks = board.Pieces[offset + 3];
            ulong friendlyPawns = board.Pieces[offset + 0];
            ulong oppPawns = board.Pieces[(isWhite ? 6 : 0) + 0];

            while (rooks != 0)
            {
                int sq = Bitboards.PopLSB(ref rooks);
                int file = sq % 8;
                ulong fileMask = FileMasks[file];

                bool noFriendlyPawn = (friendlyPawns & fileMask) == 0;
                bool noOppPawn = (oppPawns & fileMask) == 0;

                if (noFriendlyPawn && noOppPawn)
                {
                    mg += 25; eg += 15; // Open file
                }
                else if (noFriendlyPawn)
                {
                    mg += 15; eg += 8; // Semi-open file
                }

                // Rook on 7th rank (white) or 2nd rank (black)
                int rank = sq / 8;
                bool onSeventh = isWhite ? rank == 6 : rank == 1;
                if (onSeventh)
                {
                    mg += 20; eg += 30;
                }
            }
            return (mg, eg);
        }
    }
}
