using System;

namespace Chess.Engine.Core
{
    public enum MoveFlag : byte
    {
        Quiet = 0,
        DoublePawnPush = 1,
        KingsideCastle = 2,
        QueensideCastle = 3,
        Capture = 4,
        EnPassant = 5,
        PromoteToKnight = 8,
        PromoteToBishop = 9,
        PromoteToRook = 10,
        PromoteToQueen = 11,
        PromoteToKnightCapture = 12,
        PromoteToBishopCapture = 13,
        PromoteToRookCapture = 14,
        PromoteToQueenCapture = 15
    }

    public readonly struct Move
    {
        private readonly int _value;

        public static readonly Move None = new Move(0);

        public Move(int value)
        {
            _value = value;
        }

        public Move(int from, int to, MoveFlag flag, PieceType captured = PieceType.None, PieceType promotion = PieceType.None)
        {
            int capVal = captured == PieceType.None ? 0 : (int)captured + 1;
            int promVal = promotion == PieceType.None ? 0 : (int)promotion + 1;

            _value = from | (to << 6) | ((int)flag << 12) | (capVal << 16) | (promVal << 20);
        }

        public int From => _value & 0x3F;
        public int To => (_value >> 6) & 0x3F;
        public MoveFlag Flag => (MoveFlag)((_value >> 12) & 0x0F);
        public PieceType Captured => ((_value >> 16) & 0x0F) == 0 ? PieceType.None : (PieceType)(((_value >> 16) & 0x0F) - 1);
        public PieceType Promotion => ((_value >> 20) & 0x0F) == 0 ? PieceType.None : (PieceType)(((_value >> 20) & 0x0F) - 1);

        public bool IsCapture => Flag == MoveFlag.Capture || Flag == MoveFlag.EnPassant || Flag >= MoveFlag.PromoteToKnightCapture;
        public bool IsPromotion => Flag >= MoveFlag.PromoteToKnight;
        public int Value => _value;

        public static bool operator ==(Move a, Move b) => a._value == b._value;
        public static bool operator !=(Move a, Move b) => a._value != b._value;

        public override bool Equals(object? obj) => obj is Move m && m._value == _value;
        public override int GetHashCode() => _value;

        public override string ToString()
        {
            if (_value == 0) return "none";
            
            string fromStr = SquareToString(From);
            string toStr = SquareToString(To);
            string promStr = "";
            
            if (IsPromotion)
            {
                var p = Promotion;
                if (p == PieceType.WhiteKnight || p == PieceType.BlackKnight) promStr = "n";
                else if (p == PieceType.WhiteBishop || p == PieceType.BlackBishop) promStr = "b";
                else if (p == PieceType.WhiteRook || p == PieceType.BlackRook) promStr = "r";
                else if (p == PieceType.WhiteQueen || p == PieceType.BlackQueen) promStr = "q";
            }
            
            return fromStr + toStr + promStr;
        }

        private static string SquareToString(int sq)
        {
            int file = sq % 8;
            int rank = sq / 8;
            return $"{(char)('a' + file)}{rank + 1}";
        }
    }
}
