using System.IO;
using System.Text;

namespace Bitemporal
{
    public static class Serialize
    {
        static uint VarintGet(Stream s)
        {
            uint i = 0;
            while (true)
            {
                var b = (uint)s.ReadByte();
                if (b < 128u) return i + b;
                i = (i + (b & 127u)) << 7;
            }
        }

        static void VarintSet(Stream s, uint val)
        {
            if (val < 128u) s.WriteByte((byte)val);
            else if (val < 0x4000u)
            {
                s.WriteByte((byte)((val >> 7) | 128u));
                s.WriteByte((byte)(val & 127u));
            }
            else if (val < 0x200000u)
            {
                s.WriteByte((byte)((val >> 14) | 128u));
                s.WriteByte((byte)((val >> 7) | 128u));
                s.WriteByte((byte)(val & 127u));
            }
            else if (val < 0x10000000u)
            {
                s.WriteByte((byte)((val >> 21) | 128u));
                s.WriteByte((byte)((val >> 14) | 128u));
                s.WriteByte((byte)((val >> 7) | 128u));
                s.WriteByte((byte)(val & 127u));
            }
            else
            {
                s.WriteByte((byte)((val >> 28) | 128u));
                s.WriteByte((byte)((val >> 21) | 128u));
                s.WriteByte((byte)((val >> 14) | 128u));
                s.WriteByte((byte)((val >> 7) | 128u));
                s.WriteByte((byte)(val & 127u));
            }
        }

        public static SetSlim<string> SetSlimStringGet(Stream s)
        {
            var l = VarintGet(s);
            var r = new SetSlim<string>((int)l);
            for (int i = 0; i < l; i++)
            {
                var bs = new byte[VarintGet(s)];
                s.Read(bs, 0, bs.Length);
                r.Add(Encoding.UTF8.GetString(bs));
            }
            return r;
        }

        public static void SetSlimStringSet(Stream s, SetSlim<string> v)
        {
            VarintSet(s, (uint)v.Count);
            for (int i = 0; i < v.Count; i++)
            {
                var bs = Encoding.UTF8.GetBytes(v[i]);
                VarintSet(s, (uint)bs.Length);
                s.Write(bs, 0, bs.Length);
            }
        }

        public static uint[] UIntArrayGet(Stream s)
        {
            var a = new uint[VarintGet(s)];
            for (int i = 0; i < a.Length; i++)
                a[i] = VarintGet(s);
            return a;
        }

        public static void UIntArraySet(Stream s, uint[] v)
        {
            VarintSet(s, (uint)v.Length);
            for (int i = 0; i < v.Length; i++)
                VarintSet(s, v[i]);
        }

        static DataSeries DataSeriesGet(Stream s)
        {
            var l = VarintGet(s);
            if (l == 0) return default;
            var data = new byte[l];
            s.Read(data, 0, data.Length);
            return new(data);
        }

        static void DataSeriesSet(Stream s, DataSeries v)
        {
            if (v.data is null) VarintSet(s, 0);
            else
            {
                VarintSet(s, (uint)v.data.Length);
                s.Write(v!.data, 0, v!.data.Length);
            }
        }

        public static DataSeries[] DataSeriesArrayGet(Stream s, uint length)
        {
            var a = new DataSeries[length];
            for (int i = 0; i < a.Length; i++)
                a[i] = DataSeriesGet(s);
            return a;
        }

        public static void DataSeriesArraySet(Stream s, DataSeries[] v)
        {
            for (int i = 0; i < v.Length; i++)
                DataSeriesSet(s, v[i]);
        }
    }
}