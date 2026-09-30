namespace Chess.Engine.Core
{
    public static class Zobrist
    {
        public static readonly ulong[,] PieceKeys = new ulong[12, 64];
        public static readonly ulong SideKey;
        public static readonly ulong[] CastleKeys = new ulong[16];
        public static readonly ulong[] EnPassantFileKeys = new ulong[8];

        static Zobrist()
        {
            // Seeded xorshift64 PRNG for deterministic hash keys
            ulong state = 0x123456789ABCDEF0UL;
            ulong NextRandom()
            {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                return state;
            }

            for (int piece = 0; piece < 12; piece++)
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    PieceKeys[piece, sq] = NextRandom();
                }
            }

            SideKey = NextRandom();

            for (int i = 0; i < 16; i++)
            {
                CastleKeys[i] = NextRandom();
            }

            for (int i = 0; i < 8; i++)
            {
                EnPassantFileKeys[i] = NextRandom();
            }
        }
    }
}
