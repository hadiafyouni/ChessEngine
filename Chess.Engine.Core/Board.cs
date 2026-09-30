using System;

namespace Chess.Engine.Core
{
    public class Board
    {
        public ulong[] Pieces { get; } = new ulong[12];
        public ulong Occupied { get; private set; }
        public ulong WhiteOccupied { get; private set; }
        public ulong BlackOccupied { get; private set; }
        public bool WhiteToMove { get; set; } = true;
        public int EnPassantSquare { get; set; } = -1; // -1 if none
        public byte CastlingRights { get; set; } = 15; // 4 bits: WhiteKingside=1, WhiteQueenside=2, BlackKingside=4, BlackQueenside=8
        public int HalfMoveClock { get; set; } = 0;
        public int FullMoveNumber { get; set; } = 1;
        public ulong ZobristHash { get; private set; }
        private readonly PieceType[] _mailbox = new PieceType[64];

        private static readonly byte[] CastlingRightsUpdateMask = new byte[64] {
            13, 15, 15, 15, 12, 15, 15, 14, // 0..7
            15, 15, 15, 15, 15, 15, 15, 15, // 8..15
            15, 15, 15, 15, 15, 15, 15, 15, // 16..23
            15, 15, 15, 15, 15, 15, 15, 15, // 24..31
            15, 15, 15, 15, 15, 15, 15, 15, // 32..39
            15, 15, 15, 15, 15, 15, 15, 15, // 40..47
            15, 15, 15, 15, 15, 15, 15, 15, // 48..55
             7, 15, 15, 15,  3, 15, 15, 11  // 56..63
        };

        public Board()
        {
            Array.Fill(_mailbox, PieceType.None);
            ResetToStartingPosition();
        }

        private Board(bool initializeEmpty)
        {
            Array.Fill(_mailbox, PieceType.None);
            // Bypasses ResetToStartingPosition to avoid infinite recursion
        }

        public void ResetToStartingPosition()
        {
            // Clear pieces
            Array.Clear(Pieces, 0, 12);
            
            // Set starting FEN
            string startFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            var temp = FromFEN(startFEN);
            
            Array.Copy(temp.Pieces, Pieces, 12);
            Occupied = temp.Occupied;
            WhiteOccupied = temp.WhiteOccupied;
            BlackOccupied = temp.BlackOccupied;
            WhiteToMove = temp.WhiteToMove;
            EnPassantSquare = temp.EnPassantSquare;
            CastlingRights = temp.CastlingRights;
            HalfMoveClock = temp.HalfMoveClock;
            FullMoveNumber = temp.FullMoveNumber;
            ZobristHash = temp.ZobristHash;

            RebuildMailbox();
        }

        public BoardState MakeMove(Move m)
        {
            BoardState saved = new BoardState(EnPassantSquare, CastlingRights, HalfMoveClock, FullMoveNumber, ZobristHash);

            int from = m.From;
            int to = m.To;
            MoveFlag flag = m.Flag;
            PieceType captured = m.Captured;
            PieceType promotion = m.Promotion;

            PieceType movingPiece = GetPieceAt(from);

            // 1. Update active color hash
            ZobristHash ^= Zobrist.SideKey;

            // 2. XOR out old castling rights and old EP
            ZobristHash ^= Zobrist.CastleKeys[CastlingRights];
            if (EnPassantSquare != -1)
            {
                ZobristHash ^= Zobrist.EnPassantFileKeys[EnPassantSquare % 8];
            }

            // Reset EP
            EnPassantSquare = -1;

            // 3. Update half-move clock
            if (movingPiece == PieceType.WhitePawn || movingPiece == PieceType.BlackPawn || m.IsCapture)
            {
                HalfMoveClock = 0;
            }
            else
            {
                HalfMoveClock++;
            }

            // Apply move
            switch (flag)
            {
                case MoveFlag.Quiet:
                    MovePiece(movingPiece, from, to);
                    break;

                case MoveFlag.DoublePawnPush:
                    MovePiece(movingPiece, from, to);
                    EnPassantSquare = (from + to) / 2;
                    ZobristHash ^= Zobrist.EnPassantFileKeys[EnPassantSquare % 8];
                    break;

                case MoveFlag.KingsideCastle:
                    MovePiece(movingPiece, from, to);
                    if (WhiteToMove)
                        MovePiece(PieceType.WhiteRook, 7, 5); // H1 -> F1
                    else
                        MovePiece(PieceType.BlackRook, 63, 61); // H8 -> F8
                    break;

                case MoveFlag.QueensideCastle:
                    MovePiece(movingPiece, from, to);
                    if (WhiteToMove)
                        MovePiece(PieceType.WhiteRook, 0, 3); // A1 -> D1
                    else
                        MovePiece(PieceType.BlackRook, 56, 59); // A8 -> D8
                    break;

                case MoveFlag.Capture:
                    RemovePiece(captured, to);
                    MovePiece(movingPiece, from, to);
                    break;

                case MoveFlag.EnPassant:
                    int epCapSq = WhiteToMove ? to - 8 : to + 8;
                    RemovePiece(captured, epCapSq);
                    MovePiece(movingPiece, from, to);
                    break;

                case MoveFlag.PromoteToKnight:
                case MoveFlag.PromoteToBishop:
                case MoveFlag.PromoteToRook:
                case MoveFlag.PromoteToQueen:
                    RemovePiece(movingPiece, from);
                    AddPiece(promotion, to);
                    break;

                case MoveFlag.PromoteToKnightCapture:
                case MoveFlag.PromoteToBishopCapture:
                case MoveFlag.PromoteToRookCapture:
                case MoveFlag.PromoteToQueenCapture:
                    RemovePiece(movingPiece, from);
                    RemovePiece(captured, to);
                    AddPiece(promotion, to);
                    break;
            }

            // Update castling rights
            CastlingRights &= CastlingRightsUpdateMask[from];
            CastlingRights &= CastlingRightsUpdateMask[to];

            // XOR in new castling rights
            ZobristHash ^= Zobrist.CastleKeys[CastlingRights];

            // Toggle turn & fullmove
            if (!WhiteToMove)
            {
                FullMoveNumber++;
            }
            WhiteToMove = !WhiteToMove;

            // Verify hash incrementally matches from-scratch hash in debug builds
            #if DEBUG
            System.Diagnostics.Debug.Assert(ZobristHash == ComputeZobristHash(), $"Zobrist hash mismatch! Incremental={ZobristHash:X16}, Scratch={ComputeZobristHash():X16}");
            #endif

            return saved;
        }

        public void UnmakeMove(Move m, BoardState saved)
        {
            int from = m.From;
            int to = m.To;
            MoveFlag flag = m.Flag;
            PieceType captured = m.Captured;
            PieceType promotion = m.Promotion;

            WhiteToMove = !WhiteToMove;
            if (!WhiteToMove)
            {
                FullMoveNumber--;
            }

            PieceType movingPiece = promotion != PieceType.None ? promotion : GetPieceAt(to);

            switch (flag)
            {
                case MoveFlag.Quiet:
                case MoveFlag.DoublePawnPush:
                    MovePiece(movingPiece, to, from);
                    break;

                case MoveFlag.KingsideCastle:
                    MovePiece(movingPiece, to, from);
                    if (WhiteToMove)
                        MovePiece(PieceType.WhiteRook, 5, 7); // F1 -> H1
                    else
                        MovePiece(PieceType.BlackRook, 61, 63); // F8 -> H8
                    break;

                case MoveFlag.QueensideCastle:
                    MovePiece(movingPiece, to, from);
                    if (WhiteToMove)
                        MovePiece(PieceType.WhiteRook, 3, 0); // D1 -> A1
                    else
                        MovePiece(PieceType.BlackRook, 59, 56); // D8 -> A8
                    break;

                case MoveFlag.Capture:
                    MovePiece(movingPiece, to, from);
                    AddPiece(captured, to);
                    break;

                case MoveFlag.EnPassant:
                    MovePiece(movingPiece, to, from);
                    int epCapSq = WhiteToMove ? to - 8 : to + 8;
                    AddPiece(captured, epCapSq);
                    break;

                case MoveFlag.PromoteToKnight:
                case MoveFlag.PromoteToBishop:
                case MoveFlag.PromoteToRook:
                case MoveFlag.PromoteToQueen:
                    RemovePiece(movingPiece, to);
                    AddPiece(WhiteToMove ? PieceType.WhitePawn : PieceType.BlackPawn, from);
                    break;

                case MoveFlag.PromoteToKnightCapture:
                case MoveFlag.PromoteToBishopCapture:
                case MoveFlag.PromoteToRookCapture:
                case MoveFlag.PromoteToQueenCapture:
                    RemovePiece(movingPiece, to);
                    AddPiece(WhiteToMove ? PieceType.WhitePawn : PieceType.BlackPawn, from);
                    AddPiece(captured, to);
                    break;
            }

            // Restore state
            EnPassantSquare = saved.EnPassantSquare;
            CastlingRights = saved.CastlingRights;
            HalfMoveClock = saved.HalfMoveClock;
            FullMoveNumber = saved.FullMoveNumber;
            ZobristHash = saved.ZobristHash;

            #if DEBUG
            System.Diagnostics.Debug.Assert(ZobristHash == ComputeZobristHash(), $"Zobrist hash mismatch on UnmakeMove! Incremental={ZobristHash:X16}, Scratch={ComputeZobristHash():X16}");
            #endif
        }

        public PieceType GetPieceAt(int sq)
        {
            return _mailbox[sq];
        }

        public bool IsInCheck(bool white)
        {
            int kingSq = Bitboards.LSB(Pieces[white ? (int)PieceType.WhiteKing : (int)PieceType.BlackKing]);
            if (kingSq >= 64) return false;
            return IsSquareAttacked(kingSq, !white);
        }

        public bool IsSquareAttacked(int sq, bool attackedByWhite)
        {
            int oppColorOffset = attackedByWhite ? 0 : 6;

            // Pawns
            ulong oppPawns = Pieces[oppColorOffset + 0];
            ulong pawnAttacks = Bitboards.PawnAttacks[attackedByWhite ? 1 : 0][sq];
            if ((pawnAttacks & oppPawns) != 0) return true;

            // Knights
            ulong oppKnights = Pieces[oppColorOffset + 1];
            if ((Bitboards.KnightAttacks[sq] & oppKnights) != 0) return true;

            // King
            ulong oppKing = Pieces[oppColorOffset + 5];
            if ((Bitboards.KingAttacks[sq] & oppKing) != 0) return true;

            // Bishops & Queens
            ulong oppBishops = Pieces[oppColorOffset + 2];
            ulong oppQueens = Pieces[oppColorOffset + 4];
            ulong slidersDiag = oppBishops | oppQueens;
            if (slidersDiag != 0 && (Bitboards.GetBishopAttacks(sq, Occupied) & slidersDiag) != 0) return true;

            // Rooks & Queens
            ulong oppRooks = Pieces[oppColorOffset + 3];
            ulong slidersStraight = oppRooks | oppQueens;
            if (slidersStraight != 0 && (Bitboards.GetRookAttacks(sq, Occupied) & slidersStraight) != 0) return true;

            return false;
        }

        private void AddPiece(PieceType piece, int sq)
        {
            ulong bit = 1UL << sq;
            Pieces[(int)piece] |= bit;
            Occupied |= bit;
            if ((int)piece < 6)
                WhiteOccupied |= bit;
            else
                BlackOccupied |= bit;

            ZobristHash ^= Zobrist.PieceKeys[(int)piece, sq];
            _mailbox[sq] = piece;
        }

        private void RemovePiece(PieceType piece, int sq)
        {
            ulong bit = 1UL << sq;
            Pieces[(int)piece] &= ~bit;
            Occupied &= ~bit;
            if ((int)piece < 6)
                WhiteOccupied &= ~bit;
            else
                BlackOccupied &= ~bit;

            ZobristHash ^= Zobrist.PieceKeys[(int)piece, sq];
            _mailbox[sq] = PieceType.None;
        }

        private void MovePiece(PieceType piece, int from, int to)
        {
            ulong fromBit = 1UL << from;
            ulong toBit = 1UL << to;
            ulong mask = fromBit | toBit;

            Pieces[(int)piece] ^= mask;
            Occupied ^= mask;
            if ((int)piece < 6)
                WhiteOccupied ^= mask;
            else
                BlackOccupied ^= mask;

            ZobristHash ^= Zobrist.PieceKeys[(int)piece, from];
            ZobristHash ^= Zobrist.PieceKeys[(int)piece, to];
            _mailbox[from] = PieceType.None;
            _mailbox[to] = piece;
        }

        /// <summary>
        /// Makes a "null move" — passes the turn without moving any piece.
        /// Used by null-move pruning in the search. Updates Zobrist hash correctly.
        /// </summary>
        public BoardState MakeNullMove()
        {
            var saved = new BoardState(EnPassantSquare, CastlingRights, HalfMoveClock, FullMoveNumber, ZobristHash);

            ZobristHash ^= Zobrist.SideKey;
            if (EnPassantSquare != -1)
            {
                ZobristHash ^= Zobrist.EnPassantFileKeys[EnPassantSquare % 8];
                EnPassantSquare = -1;
            }
            WhiteToMove = !WhiteToMove;

            return saved;
        }

        /// <summary>
        /// Undoes a null move, restoring the saved state.
        /// </summary>
        public void UnmakeNullMove(BoardState saved)
        {
            WhiteToMove = !WhiteToMove;
            EnPassantSquare = saved.EnPassantSquare;
            CastlingRights = saved.CastlingRights;
            HalfMoveClock = saved.HalfMoveClock;
            FullMoveNumber = saved.FullMoveNumber;
            ZobristHash = saved.ZobristHash;
        }

        public void UpdateOccupancies()
        {
            WhiteOccupied = Pieces[0] | Pieces[1] | Pieces[2] | Pieces[3] | Pieces[4] | Pieces[5];
            BlackOccupied = Pieces[6] | Pieces[7] | Pieces[8] | Pieces[9] | Pieces[10] | Pieces[11];
            Occupied = WhiteOccupied | BlackOccupied;
        }

        public ulong ComputeZobristHash()
        {
            ulong hash = 0;

            for (int p = 0; p < 12; p++)
            {
                ulong bb = Pieces[p];
                while (bb != 0)
                {
                    int sq = Bitboards.PopLSB(ref bb);
                    hash ^= Zobrist.PieceKeys[p, sq];
                }
            }

            if (WhiteToMove)
            {
                hash ^= Zobrist.SideKey;
            }

            hash ^= Zobrist.CastleKeys[CastlingRights];

            if (EnPassantSquare != -1)
            {
                hash ^= Zobrist.EnPassantFileKeys[EnPassantSquare % 8];
            }

            return hash;
        }

        public static Board FromFEN(string fen)
        {
            Board board = new Board(true);
            string[] parts = fen.Split(' ');

            // Clear pieces
            Array.Clear(board.Pieces, 0, 12);

            // 1. Piece placement
            string[] ranks = parts[0].Split('/');
            for (int r = 0; r < 8; r++)
            {
                int rank = 7 - r;
                int file = 0;
                foreach (char c in ranks[r])
                {
                    if (char.IsDigit(c))
                    {
                        file += (c - '0');
                    }
                    else
                    {
                        PieceType piece = CharToPiece(c);
                        int sq = rank * 8 + file;
                        board.Pieces[(int)piece] |= (1UL << sq);
                        file++;
                    }
                }
            }

            board.UpdateOccupancies();

            // 2. Turn
            board.WhiteToMove = parts[1] == "w";

            // 3. Castling rights
            board.CastlingRights = 0;
            if (parts.Length > 2 && parts[2] != "-")
            {
                foreach (char c in parts[2])
                {
                    if (c == 'K') board.CastlingRights |= 1;
                    else if (c == 'Q') board.CastlingRights |= 2;
                    else if (c == 'k') board.CastlingRights |= 4;
                    else if (c == 'q') board.CastlingRights |= 8;
                }
            }

            // 4. En Passant
            board.EnPassantSquare = -1;
            if (parts.Length > 3 && parts[3] != "-")
            {
                string epStr = parts[3];
                int file = epStr[0] - 'a';
                int rank = epStr[1] - '1';
                board.EnPassantSquare = rank * 8 + file;
            }

            // 5. Halfmove clock
            board.HalfMoveClock = 0;
            if (parts.Length > 4)
            {
                int.TryParse(parts[4], out int halfClock);
                board.HalfMoveClock = halfClock;
            }

            // 6. Fullmove number
            board.FullMoveNumber = 1;
            if (parts.Length > 5)
            {
                int.TryParse(parts[5], out int fullNum);
                board.FullMoveNumber = fullNum;
            }

            board.ZobristHash = board.ComputeZobristHash();
            board.RebuildMailbox();

            return board;
        }

        public string ToFEN()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int r = 7; r >= 0; r--)
            {
                int emptyCount = 0;
                for (int f = 0; f < 8; f++)
                {
                    int sq = r * 8 + f;
                    PieceType p = GetPieceAt(sq);
                    if (p == PieceType.None)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0)
                        {
                            sb.Append(emptyCount);
                            emptyCount = 0;
                        }
                        sb.Append(PieceToChar(p));
                    }
                }
                if (emptyCount > 0)
                {
                    sb.Append(emptyCount);
                }
                if (r > 0)
                {
                    sb.Append('/');
                }
            }

            sb.Append(' ');
            sb.Append(WhiteToMove ? 'w' : 'b');

            sb.Append(' ');
            if (CastlingRights == 0)
            {
                sb.Append('-');
            }
            else
            {
                if ((CastlingRights & 1) != 0) sb.Append('K');
                if ((CastlingRights & 2) != 0) sb.Append('Q');
                if ((CastlingRights & 4) != 0) sb.Append('k');
                if ((CastlingRights & 8) != 0) sb.Append('q');
            }

            sb.Append(' ');
            if (EnPassantSquare == -1)
            {
                sb.Append('-');
            }
            else
            {
                int file = EnPassantSquare % 8;
                int rank = EnPassantSquare / 8;
                sb.Append($"{(char)('a' + file)}{rank + 1}");
            }

            sb.Append(' ');
            sb.Append(HalfMoveClock);

            sb.Append(' ');
            sb.Append(FullMoveNumber);

            return sb.ToString();
        }

        private static PieceType CharToPiece(char c)
        {
            return c switch
            {
                'P' => PieceType.WhitePawn,
                'N' => PieceType.WhiteKnight,
                'B' => PieceType.WhiteBishop,
                'R' => PieceType.WhiteRook,
                'Q' => PieceType.WhiteQueen,
                'K' => PieceType.WhiteKing,
                'p' => PieceType.BlackPawn,
                'n' => PieceType.BlackKnight,
                'b' => PieceType.BlackBishop,
                'r' => PieceType.BlackRook,
                'q' => PieceType.BlackQueen,
                'k' => PieceType.BlackKing,
                _ => PieceType.None
            };
        }

        private static char PieceToChar(PieceType p)
        {
            return p switch
            {
                PieceType.WhitePawn => 'P',
                PieceType.WhiteKnight => 'N',
                PieceType.WhiteBishop => 'B',
                PieceType.WhiteRook => 'R',
                PieceType.WhiteQueen => 'Q',
                PieceType.WhiteKing => 'K',
                PieceType.BlackPawn => 'p',
                PieceType.BlackKnight => 'n',
                PieceType.BlackBishop => 'b',
                PieceType.BlackRook => 'r',
                PieceType.BlackQueen => 'q',
                PieceType.BlackKing => 'k',
                _ => '?'
            };
        }

        private void RebuildMailbox()
        {
            Array.Fill(_mailbox, PieceType.None);
            for (int p = 0; p < 12; p++)
            {
                ulong bb = Pieces[p];
                while (bb != 0)
                {
                    int sq = Bitboards.PopLSB(ref bb);
                    _mailbox[sq] = (PieceType)p;
                }
            }
        }
    }
}
