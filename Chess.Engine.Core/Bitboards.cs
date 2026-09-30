using System;
using System.Collections.Generic;
using System.Numerics;

namespace Chess.Engine.Core
{
    public static class Bitboards
    {
        // Precomputed attacks
        public static readonly ulong[] KnightAttacks = new ulong[64];
        public static readonly ulong[] KingAttacks = new ulong[64];
        public static readonly ulong[][] PawnAttacks = new ulong[2][]
        {
            new ulong[64], // White pawn attacks
            new ulong[64]  // Black pawn attacks
        };

        // Magic tables
        public static readonly ulong[] RookMasks = new ulong[64];
        public static readonly ulong[] BishopMasks = new ulong[64];
        public static readonly int[] RookShifts = new int[64];
        public static readonly int[] BishopShifts = new int[64];
        public static readonly int[] RookOffsets = new int[64];
        public static readonly int[] BishopOffsets = new int[64];

        public static readonly ulong[] RookAttackTable = new ulong[102400];
        public static readonly ulong[] BishopAttackTable = new ulong[5248];

        // Bit helpers
        public static int LSB(ulong bb) => BitOperations.TrailingZeroCount(bb);

        public static int PopLSB(ref ulong bb)
        {
            int index = BitOperations.TrailingZeroCount(bb);
            bb &= bb - 1;
            return index;
        }

        public static int PopCount(ulong bb) => BitOperations.PopCount(bb);

        // Magic constants (Stockfish / Romstad)
        public static readonly ulong[] RookMagics = new ulong[64] {
            0x80021424824000UL,             0x240004220011000UL,             0x2080200008801000UL,             0x1180080004100080UL,
            0x280080081040002UL,             0x600020090080144UL,             0x210024c5000c0200UL,             0x200020043240089UL,
            0x40800080400020UL,             0x6010404010002000UL,             0x1121002000410018UL,             0x96004020902a00UL,
            0xe02802402280080UL,             0x12000402011008UL,             0x8003010001020004UL,             0x21800141002080UL,
            0x208208008400080UL,             0x8a90104020004000UL,             0x800888020001000UL,             0x4818010100201000UL,
            0x4920808004000800UL,             0x24004002004100UL,             0x600040041902208UL,             0x12200008cd401UL,
            0x4040802080004000UL,             0x8220008280400024UL,             0x240100080200080UL,             0x4104200082201UL,
            0x84110100040800UL,             0x100020080800400UL,             0x4001003900840a00UL,             0x2000010200006084UL,
            0x410040028680002aUL,             0x5050082000c00140UL,             0x802004082001020UL,             0x208008188801001UL,
            0x48800800800400UL,             0x10a2000401010008UL,             0x1000100854000201UL,             0x414004a000089UL,
            0xc0400030808000UL,             0x10004020014004UL,             0x800108042020020UL,             0x8100401022020008UL,
            0x6040008008080UL,             0x232002010040400UL,             0x1010210040008UL,             0x4040008120420014UL,
            0x308000410100UL,             0x4820420020810200UL,             0xc20080040100040UL,             0x4090008010080080UL,
            0x1024800800040080UL,             0x402800400020080UL,             0x1c10800100020080UL,             0x800100016280UL,
            0x1000201881004202UL,             0x4400280215901UL,             0x200040090213UL,             0x184520900101UL,
            0xa006010040806UL,             0x1009000208040001UL,             0x810010810d204UL,             0x581008410402102UL
        };

        public static readonly ulong[] BishopMagics = new ulong[64] {
            0x91022a04040011UL,             0x2042d08109010000UL,             0x8110840040400008UL,             0x42208202000040UL,
            0x84504100002021UL,             0x280882008101801UL,             0x4401281210040800UL,             0x101a090086800UL,
            0x2208310020090UL,             0x2000041000a10104UL,             0x488080224002400UL,             0xd1040400880002UL,
            0x4020040420000002UL,             0x1042420080910UL,             0x2011810101202001UL,             0x1008a012050UL,
            0x20c1c008011120UL,             0x13a02008010900UL,             0x2400c641020200UL,             0x128000222044001UL,
            0x124002080e01000UL,             0x440a000041100100UL,             0x12000402010431UL,             0x4a8100582400UL,
            0x4a08080121200100UL,             0x9600408880908UL,             0x4881010002021UL,             0x420080001040408UL,
            0x50101009004000UL,             0x108810006004201UL,             0x8101010002080111UL,             0x8200810000243200UL,
            0x8086402082100UL,             0x1280c4100a200200UL,             0x4004808040720UL,             0x8002010040040040UL,
            0x240020200402080UL,             0x400820080841000UL,             0x8141420081020800UL,             0x1214104040020100UL,
            0x20402084082040aUL,             0x92011008004230UL,             0x820802008100UL,             0x5000c204804800UL,
            0x8000220204100a02UL,             0x4c40300040800110UL,             0x2010308100408104UL,             0x8008401500380UL,
            0x802011002100002UL,             0x1001a40108290100UL,             0x4200900003UL,             0x410000210540200UL,
            0x100400a1204d0008UL,             0x4000080208020640UL,             0x1208880800840500UL,             0x804211202020000UL,
            0x8000108404200480UL,             0x100450401a031UL,             0x1010180100411046UL,             0x40002080841109UL,
            0x1201a0028030400UL,             0x2040008404181a02UL,             0x18202002008108UL,             0x90049800404200UL
        };


        private static readonly int[] RookBits = new int[64] {
            12, 11, 11, 11, 11, 11, 11, 12,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            12, 11, 11, 11, 11, 11, 11, 12
        };

        private static readonly int[] BishopBits = new int[64] {
            6, 5, 5, 5, 5, 5, 5, 6,
            5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 7, 7, 7, 7, 5, 5,
            5, 5, 7, 9, 9, 7, 5, 5,
            5, 5, 7, 9, 9, 7, 5, 5,
            5, 5, 7, 7, 7, 7, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5,
            6, 5, 5, 5, 5, 5, 5, 6
        };

        static Bitboards()
        {
            InitializeStepAttacks();
            InitializeMagicTables();
        }

        private static void InitializeStepAttacks()
        {
            // Knights, Kings, and Pawns
            for (int sq = 0; sq < 64; sq++)
            {
                int r = sq / 8;
                int f = sq % 8;

                // Knight Attacks
                ulong kn = 0;
                if (r + 2 < 8 && f + 1 < 8) kn |= 1UL << (sq + 17);
                if (r + 2 < 8 && f - 1 >= 0) kn |= 1UL << (sq + 15);
                if (r + 1 < 8 && f + 2 < 8) kn |= 1UL << (sq + 10);
                if (r + 1 < 8 && f - 2 >= 0) kn |= 1UL << (sq + 6);
                if (r - 2 >= 0 && f - 1 >= 0) kn |= 1UL << (sq - 17);
                if (r - 2 >= 0 && f + 1 < 8) kn |= 1UL << (sq - 15);
                if (r - 1 >= 0 && f - 2 >= 0) kn |= 1UL << (sq - 10);
                if (r - 1 >= 0 && f + 2 < 8) kn |= 1UL << (sq - 6);
                KnightAttacks[sq] = kn;

                // King Attacks
                ulong kg = 0;
                if (r + 1 < 8) kg |= 1UL << (sq + 8);
                if (r - 1 >= 0) kg |= 1UL << (sq - 8);
                if (f + 1 < 8) kg |= 1UL << (sq + 1);
                if (f - 1 >= 0) kg |= 1UL << (sq - 1);
                if (r + 1 < 8 && f + 1 < 8) kg |= 1UL << (sq + 9);
                if (r + 1 < 8 && f - 1 >= 0) kg |= 1UL << (sq + 7);
                if (r - 1 >= 0 && f - 1 >= 0) kg |= 1UL << (sq - 9);
                if (r - 1 >= 0 && f + 1 < 8) kg |= 1UL << (sq - 7);
                KingAttacks[sq] = kg;

                // Pawn Attacks
                // White
                ulong wPawn = 0;
                if (f > 0) wPawn |= 1UL << (sq + 7);
                if (f < 7) wPawn |= 1UL << (sq + 9);
                if (r < 7) PawnAttacks[0][sq] = wPawn;

                // Black
                ulong bPawn = 0;
                if (f > 0) bPawn |= 1UL << (sq - 9);
                if (f < 7) bPawn |= 1UL << (sq - 7);
                if (r > 0) PawnAttacks[1][sq] = bPawn;
            }
        }

        private static void InitializeMagicTables()
        {
            int rookOffset = 0;
            int bishopOffset = 0;

            for (int sq = 0; sq < 64; sq++)
            {
                RookMasks[sq] = GetRookMask(sq);
                BishopMasks[sq] = GetBishopMask(sq);

                RookShifts[sq] = 64 - RookBits[sq];
                BishopShifts[sq] = 64 - BishopBits[sq];

                RookOffsets[sq] = rookOffset;
                BishopOffsets[sq] = bishopOffset;

                // Build Rook attack table for this square
                ulong rMask = RookMasks[sq];
                ulong[] rSubsets = CreateOccupancySubsets(rMask);
                foreach (ulong occ in rSubsets)
                {
                    int index = (int)((occ * RookMagics[sq]) >> RookShifts[sq]);
                    RookAttackTable[rookOffset + index] = GenerateRookAttacks(sq, occ);
                }

                // Build Bishop attack table for this square
                ulong bMask = BishopMasks[sq];
                ulong[] bSubsets = CreateOccupancySubsets(bMask);
                foreach (ulong occ in bSubsets)
                {
                    int index = (int)((occ * BishopMagics[sq]) >> BishopShifts[sq]);
                    BishopAttackTable[bishopOffset + index] = GenerateBishopAttacks(sq, occ);
                }

                rookOffset += 1 << RookBits[sq];
                bishopOffset += 1 << BishopBits[sq];
            }
        }

        public static ulong GetRookMask(int sq)
        {
            ulong mask = 0;
            int r = sq / 8;
            int f = sq % 8;
            for (int i = 1; i < 7; i++)
            {
                if (i != r) mask |= 1UL << (i * 8 + f);
            }
            for (int i = 1; i < 7; i++)
            {
                if (i != f) mask |= 1UL << (r * 8 + i);
            }
            return mask;
        }

        public static ulong GetBishopMask(int sq)
        {
            ulong mask = 0;
            int r = sq / 8;
            int f = sq % 8;
            for (int i = 1; r + i < 7 && f + i < 7; i++) mask |= 1UL << ((r + i) * 8 + (f + i));
            for (int i = 1; r + i < 7 && f - i > 0; i++) mask |= 1UL << ((r + i) * 8 + (f - i));
            for (int i = 1; r - i > 0 && f + i < 7; i++) mask |= 1UL << ((r - i) * 8 + (f + i));
            for (int i = 1; r - i > 0 && f - i > 0; i++) mask |= 1UL << ((r - i) * 8 + (f - i));
            return mask;
        }

        public static ulong GenerateRookAttacks(int sq, ulong occ)
        {
            ulong attacks = 0;
            int r = sq / 8;
            int f = sq % 8;
            // Up
            for (int i = r + 1; i < 8; i++)
            {
                attacks |= 1UL << (i * 8 + f);
                if ((occ & (1UL << (i * 8 + f))) != 0) break;
            }
            // Down
            for (int i = r - 1; i >= 0; i--)
            {
                attacks |= 1UL << (i * 8 + f);
                if ((occ & (1UL << (i * 8 + f))) != 0) break;
            }
            // Right
            for (int i = f + 1; i < 8; i++)
            {
                attacks |= 1UL << (r * 8 + i);
                if ((occ & (1UL << (r * 8 + i))) != 0) break;
            }
            // Left
            for (int i = f - 1; i >= 0; i--)
            {
                attacks |= 1UL << (r * 8 + i);
                if ((occ & (1UL << (r * 8 + i))) != 0) break;
            }
            return attacks;
        }

        public static ulong GenerateBishopAttacks(int sq, ulong occ)
        {
            ulong attacks = 0;
            int r = sq / 8;
            int f = sq % 8;
            // Up-Right
            for (int i = 1; r + i < 8 && f + i < 8; i++)
            {
                attacks |= 1UL << ((r + i) * 8 + (f + i));
                if ((occ & (1UL << ((r + i) * 8 + (f + i)))) != 0) break;
            }
            // Up-Left
            for (int i = 1; r + i < 8 && f - i >= 0; i++)
            {
                attacks |= 1UL << ((r + i) * 8 + (f - i));
                if ((occ & (1UL << ((r + i) * 8 + (f - i)))) != 0) break;
            }
            // Down-Right
            for (int i = 1; r - i >= 0 && f + i < 8; i++)
            {
                attacks |= 1UL << ((r - i) * 8 + (f + i));
                if ((occ & (1UL << ((r - i) * 8 + (f + i)))) != 0) break;
            }
            // Down-Left
            for (int i = 1; r - i >= 0 && f - i >= 0; i++)
            {
                attacks |= 1UL << ((r - i) * 8 + (f - i));
                if ((occ & (1UL << ((r - i) * 8 + (f - i)))) != 0) break;
            }
            return attacks;
        }

        public static ulong GetRookAttacks(int sq, ulong occupied)
        {
            ulong occ = occupied & RookMasks[sq];
            int index = (int)((occ * RookMagics[sq]) >> RookShifts[sq]);
            return RookAttackTable[RookOffsets[sq] + index];
        }

        public static ulong GetBishopAttacks(int sq, ulong occupied)
        {
            ulong occ = occupied & BishopMasks[sq];
            int index = (int)((occ * BishopMagics[sq]) >> BishopShifts[sq]);
            return BishopAttackTable[BishopOffsets[sq] + index];
        }

        public static ulong GetQueenAttacks(int sq, ulong occupied)
        {
            return GetRookAttacks(sq, occupied) | GetBishopAttacks(sq, occupied);
        }

        private static ulong[] CreateOccupancySubsets(ulong mask)
        {
            List<ulong> subsets = new List<ulong>();
            int[] indices = new int[64];
            int count = 0;
            ulong temp = mask;
            while (temp != 0)
            {
                indices[count++] = BitOperations.TrailingZeroCount(temp);
                temp &= temp - 1;
            }
            int numSubsets = 1 << count;
            ulong[] result = new ulong[numSubsets];
            for (int i = 0; i < numSubsets; i++)
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
    }
}
