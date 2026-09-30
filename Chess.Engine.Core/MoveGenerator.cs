using System;

namespace Chess.Engine.Core
{
    public static class MoveGenerator
    {
        [ThreadStatic]
        private static Move[]? _movePool;

        private static Move[] GetMovePool()
        {
            if (_movePool == null)
            {
                _movePool = new Move[256];
            }
            return _movePool;
        }

        public static Span<Move> GenerateMoves(Board board)
        {
            var pool = GetMovePool();
            int count = GenerateMoves(board, pool, false);
            return pool.AsSpan(0, count);
        }

        public static Span<Move> GenerateCaptures(Board board)
        {
            var pool = GetMovePool();
            int count = GenerateMoves(board, pool, true);
            return pool.AsSpan(0, count);
        }

        public static int GenerateMoves(Board board, Span<Move> legalMoves, bool capturesOnly = false)
        {
            Span<Move> pseudoMoves = stackalloc Move[256];
            int pseudoCount = GeneratePseudoLegalMoves(board, pseudoMoves, capturesOnly);

            int legalCount = 0;
            for (int i = 0; i < pseudoCount; i++)
            {
                Move m = pseudoMoves[i];
                var state = board.MakeMove(m);
                
                // MakeMove toggled WhiteToMove, so the side who moved is now !board.WhiteToMove
                if (!board.IsInCheck(!board.WhiteToMove))
                {
                    legalMoves[legalCount++] = m;
                }
                
                board.UnmakeMove(m, state);
            }

            return legalCount;
        }

        public static int GeneratePseudoLegalMoves(Board board, Span<Move> moves, bool capturesOnly = false)
        {
            int count = 0;
            bool us = board.WhiteToMove;
            ulong friendlyOccupied = us ? board.WhiteOccupied : board.BlackOccupied;
            ulong opponentOccupied = us ? board.BlackOccupied : board.WhiteOccupied;

            // Generate Pawn Moves
            int usPawnOffset = us ? 0 : 6;
            ulong pawns = board.Pieces[usPawnOffset + 0];
            while (pawns != 0)
            {
                int from = Bitboards.PopLSB(ref pawns);
                
                // Pawn push
                int pushTo = us ? from + 8 : from - 8;
                if (pushTo >= 0 && pushTo < 64 && (board.Occupied & (1UL << pushTo)) == 0)
                {
                    bool isPromotion = us ? pushTo >= 56 : pushTo < 8;
                    if (isPromotion)
                    {
                        AddPawnMoves(from, pushTo, MoveFlag.PromoteToKnight, PieceType.None, moves, ref count, capturesOnly);
                    }
                    else if (!capturesOnly)
                    {
                        moves[count++] = new Move(from, pushTo, MoveFlag.Quiet);
                        
                        // Double push
                        int doublePushTo = us ? from + 16 : from - 16;
                        bool onStartRank = us ? (from >= 8 && from <= 15) : (from >= 48 && from <= 55);
                        if (onStartRank && (board.Occupied & (1UL << doublePushTo)) == 0)
                        {
                            moves[count++] = new Move(from, doublePushTo, MoveFlag.DoublePawnPush);
                        }
                    }
                }

                // Pawn captures
                ulong pawnAttacks = Bitboards.PawnAttacks[us ? 0 : 1][from];
                
                // Regular captures
                ulong captureTargets = pawnAttacks & opponentOccupied;
                while (captureTargets != 0)
                {
                    int to = Bitboards.PopLSB(ref captureTargets);
                    PieceType captured = board.GetPieceAt(to);
                    bool isPromotion = us ? to >= 56 : to < 8;
                    if (isPromotion)
                    {
                        AddPawnMoves(from, to, MoveFlag.PromoteToKnightCapture, captured, moves, ref count, capturesOnly);
                    }
                    else
                    {
                        moves[count++] = new Move(from, to, MoveFlag.Capture, captured);
                    }
                }

                // En Passant captures
                if (board.EnPassantSquare != -1 && (pawnAttacks & (1UL << board.EnPassantSquare)) != 0)
                {
                    PieceType oppPawn = us ? PieceType.BlackPawn : PieceType.WhitePawn;
                    moves[count++] = new Move(from, board.EnPassantSquare, MoveFlag.EnPassant, oppPawn);
                }
            }

            // Generate Knight Moves
            ulong knights = board.Pieces[usPawnOffset + 1];
            while (knights != 0)
            {
                int from = Bitboards.PopLSB(ref knights);
                ulong targets = Bitboards.KnightAttacks[from] & ~friendlyOccupied;
                while (targets != 0)
                {
                    int to = Bitboards.PopLSB(ref targets);
                    AddNormalMove(from, to, opponentOccupied, moves, ref count, capturesOnly, board);
                }
            }

            // Generate Bishop Moves
            ulong bishops = board.Pieces[usPawnOffset + 2];
            while (bishops != 0)
            {
                int from = Bitboards.PopLSB(ref bishops);
                ulong targets = Bitboards.GetBishopAttacks(from, board.Occupied) & ~friendlyOccupied;
                while (targets != 0)
                {
                    int to = Bitboards.PopLSB(ref targets);
                    AddNormalMove(from, to, opponentOccupied, moves, ref count, capturesOnly, board);
                }
            }

            // Generate Rook Moves
            ulong rooks = board.Pieces[usPawnOffset + 3];
            while (rooks != 0)
            {
                int from = Bitboards.PopLSB(ref rooks);
                ulong targets = Bitboards.GetRookAttacks(from, board.Occupied) & ~friendlyOccupied;
                while (targets != 0)
                {
                    int to = Bitboards.PopLSB(ref targets);
                    AddNormalMove(from, to, opponentOccupied, moves, ref count, capturesOnly, board);
                }
            }

            // Generate Queen Moves
            ulong queens = board.Pieces[usPawnOffset + 4];
            while (queens != 0)
            {
                int from = Bitboards.PopLSB(ref queens);
                ulong targets = Bitboards.GetQueenAttacks(from, board.Occupied) & ~friendlyOccupied;
                while (targets != 0)
                {
                    int to = Bitboards.PopLSB(ref targets);
                    AddNormalMove(from, to, opponentOccupied, moves, ref count, capturesOnly, board);
                }
            }

            // Generate King Moves
            ulong king = board.Pieces[usPawnOffset + 5];
            if (king != 0)
            {
                int from = Bitboards.PopLSB(ref king);
                ulong targets = Bitboards.KingAttacks[from] & ~friendlyOccupied;
                while (targets != 0)
                {
                    int to = Bitboards.PopLSB(ref targets);
                    AddNormalMove(from, to, opponentOccupied, moves, ref count, capturesOnly, board);
                }

                // Castling (only generated if capturesOnly is false and king not in check)
                if (!capturesOnly && !board.IsInCheck(us))
                {
                    if (us)
                    {
                        // White Kingside
                        if ((board.CastlingRights & 1) != 0 &&
                            (board.Occupied & ((1UL << 5) | (1UL << 6))) == 0 &&
                            !board.IsSquareAttacked(5, false) &&
                            !board.IsSquareAttacked(6, false))
                        {
                            moves[count++] = new Move(4, 6, MoveFlag.KingsideCastle);
                        }

                        // White Queenside
                        if ((board.CastlingRights & 2) != 0 &&
                            (board.Occupied & ((1UL << 1) | (1UL << 2) | (1UL << 3))) == 0 &&
                            !board.IsSquareAttacked(3, false) &&
                            !board.IsSquareAttacked(2, false))
                        {
                            moves[count++] = new Move(4, 2, MoveFlag.QueensideCastle);
                        }
                    }
                    else
                    {
                        // Black Kingside
                        if ((board.CastlingRights & 4) != 0 &&
                            (board.Occupied & ((1UL << 61) | (1UL << 62))) == 0 &&
                            !board.IsSquareAttacked(61, true) &&
                            !board.IsSquareAttacked(62, true))
                        {
                            moves[count++] = new Move(60, 62, MoveFlag.KingsideCastle);
                        }

                        // Black Queenside
                        if ((board.CastlingRights & 8) != 0 &&
                            (board.Occupied & ((1UL << 57) | (1UL << 58) | (1UL << 59))) == 0 &&
                            !board.IsSquareAttacked(59, true) &&
                            !board.IsSquareAttacked(58, true))
                        {
                            moves[count++] = new Move(60, 58, MoveFlag.QueensideCastle);
                        }
                    }
                }
            }

            return count;
        }

        private static void AddNormalMove(int from, int to, ulong opponentOccupied, Span<Move> moves, ref int count, bool capturesOnly, Board board)
        {
            if ((opponentOccupied & (1UL << to)) != 0)
            {
                PieceType cap = board.GetPieceAt(to);
                moves[count++] = new Move(from, to, MoveFlag.Capture, cap);
            }
            else if (!capturesOnly)
            {
                moves[count++] = new Move(from, to, MoveFlag.Quiet);
            }
        }

        private static void AddPawnMoves(int from, int to, MoveFlag flag, PieceType cap, Span<Move> moves, ref int count, bool capturesOnly)
        {
            bool us = to > from; // simple check: White moves up, Black moves down
            if (flag >= MoveFlag.PromoteToKnight)
            {
                if (flag == MoveFlag.PromoteToKnightCapture)
                {
                    moves[count++] = new Move(from, to, MoveFlag.PromoteToKnightCapture, cap, us ? PieceType.WhiteKnight : PieceType.BlackKnight);
                    moves[count++] = new Move(from, to, MoveFlag.PromoteToBishopCapture, cap, us ? PieceType.WhiteBishop : PieceType.BlackBishop);
                    moves[count++] = new Move(from, to, MoveFlag.PromoteToRookCapture, cap, us ? PieceType.WhiteRook : PieceType.BlackRook);
                    moves[count++] = new Move(from, to, MoveFlag.PromoteToQueenCapture, cap, us ? PieceType.WhiteQueen : PieceType.BlackQueen);
                }
                else
                {
                    if (!capturesOnly)
                    {
                        moves[count++] = new Move(from, to, MoveFlag.PromoteToKnight, PieceType.None, us ? PieceType.WhiteKnight : PieceType.BlackKnight);
                        moves[count++] = new Move(from, to, MoveFlag.PromoteToBishop, PieceType.None, us ? PieceType.WhiteBishop : PieceType.BlackBishop);
                        moves[count++] = new Move(from, to, MoveFlag.PromoteToRook, PieceType.None, us ? PieceType.WhiteRook : PieceType.BlackRook);
                        moves[count++] = new Move(from, to, MoveFlag.PromoteToQueen, PieceType.None, us ? PieceType.WhiteQueen : PieceType.BlackQueen);
                    }
                }
            }
            else
            {
                moves[count++] = new Move(from, to, flag, cap);
            }
        }
    }
}
