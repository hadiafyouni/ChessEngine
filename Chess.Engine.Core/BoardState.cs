namespace Chess.Engine.Core
{
    public struct BoardState
    {
        public int EnPassantSquare;
        public byte CastlingRights;
        public int HalfMoveClock;
        public int FullMoveNumber;
        public ulong ZobristHash;

        public BoardState(int enPassantSquare, byte castlingRights, int halfMoveClock, int fullMoveNumber, ulong zobristHash)
        {
            EnPassantSquare = enPassantSquare;
            CastlingRights = castlingRights;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
            ZobristHash = zobristHash;
        }
    }
}
