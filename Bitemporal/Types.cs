using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Bitemporal
{
    public struct UVint
    {
        public const ulong MinValue = 0UL;
        public const ulong MaxValue = (1UL << 56) - 1UL;
        public ulong I;
        public UVint(ulong i) { Debug.Assert(i >= MinValue && i <= MaxValue); I = i; }
        public static UVint operator +(UVint a, UVint b) => new(a.I + b.I);
        public static UVint operator -(UVint a, UVint b) => new(a.I - b.I);
        public static UVint operator -(UVint a, long b) => new((ulong)((long)a.I - b));
        public override string ToString() => I.ToString();
    }

    public struct Vint : IEquatable<Vint>
    {
        public const long MinValue = -1L << 54;
        public const long MaxValue = (1L << 54) - 1L;
        public long I;
        public Vint(long i) { Debug.Assert(i >= MinValue && i <= MaxValue); I = i; }
        public bool Equals(Vint other) => I == other.I;
        public override bool Equals(object? o) => o is not null && I == ((Vint)o).I;
        public override int GetHashCode() => (int)I;
        public static bool operator ==(Vint left, Vint right) => left.I == right.I;
        public static bool operator !=(Vint left, Vint right) => left.I != right.I;
        public override string ToString() => I.ToString();
    }

    public struct Bytes
    {
        public byte[] Array; // rename
        public int Position;
        public Bytes(byte[] a) { Array = a; Position = 0; }
        public static Bytes Empty() => new() { Array = ArrayPool<byte>.Shared.Rent(16) };
        public byte[] Trim() => Array[..Position];
        public void Return() => ArrayPool<byte>.Shared.Return(Array);
        public void Write(UVint v)
        {
            var noBytes = BitOperations.Log2(v.I) / 7;
            if (Position + noBytes >= Array.Length)
            {
                var newArray = ArrayPool<byte>.Shared.Rent(Array.Length * 2);
                System.Array.Copy(Array, newArray, Position);
                ArrayPool<byte>.Shared.Return(Array);
                Array = newArray;
            }
            Unsafe.WriteUnaligned(ref Array[Position], ((v.I << 1) | 1UL) << noBytes);
            Position += noBytes + 1;
        }
        public UVint Read()
        {
            ulong result = Unsafe.ReadUnaligned<ulong>(ref Array[Position]);
            var noBytes = BitOperations.TrailingZeroCount(result) + 1;
            Position += noBytes;
            return new(((1UL << (7 * noBytes)) - 1UL) & (result >> noBytes));
        }
    }

    public struct Date
    {
        const int OFFSET = 737790; // (int)(DateTime(2021, 1, 1).Ticks / TimeSpan.TicksPerDay);
        public readonly int I;
        public Date(int i) => I = i;
        public Date(DateTime dt) => I = (int)(dt.Ticks / TimeSpan.TicksPerDay) - OFFSET;
        public static bool operator <(Date a, Date b) => a.I < b.I;
        public static bool operator >(Date a, Date b) => a.I > b.I;
        public static bool operator >=(Date a, Date b) => a.I >= b.I;
        public static bool operator <=(Date a, Date b) => a.I <= b.I;
        public static int operator -(Date a, Date b) => a.I - b.I;
        public Date Add(int i) => new(I + i);
        public DateTime ToDateTime() => new((I + OFFSET) * TimeSpan.TicksPerDay);
        public override string ToString() => ToDateTime().ToString("yyyy-MM-dd");
    }

    public struct Time
    {
        const long OFFSET = 63745056000000L; // DateTime(2021, 1, 1).Ticks / TimeSpan.TicksPerMillisecond;
        public readonly long I;
        public Time(int i) => I = i;
        public Time(DateTime dt) => I = dt.Ticks / TimeSpan.TicksPerMillisecond - OFFSET;
        public static bool operator <(Time a, Time b) => a.I < b.I;
        public static bool operator >(Time a, Time b) => a.I > b.I;
        public static bool operator >=(Time a, Time b) => a.I >= b.I;
        public static bool operator <=(Time a, Time b) => a.I <= b.I;
        public DateTime ToDateTime() => new((I + OFFSET) * TimeSpan.TicksPerMillisecond);
        public Date ToDate() => new(ToDateTime());
        public override string ToString() => ToDateTime().ToString("yyyy-MM-dd HH:mm:ss.ffff");
    }

    public struct TxId
    {
        public uint I;
        public TxId(uint i) => I = i;
        public static bool operator <(TxId a, TxId b) => a.I < b.I;
        public static bool operator >(TxId a, TxId b) => a.I > b.I;
        public static bool operator >=(TxId a, TxId b) => a.I >= b.I;
        public static bool operator <=(TxId a, TxId b) => a.I <= b.I;
        public override string ToString() => I.ToString();
    }
}