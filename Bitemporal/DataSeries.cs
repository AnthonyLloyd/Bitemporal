using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bitemporal
{
    public struct DataSeries : IEnumerable<(Date, TxId, Vint)>
    {
        internal readonly byte[] data;
        internal DataSeries(byte[] d) => data = d;
        public static DataSeries Single(Date d, TxId t, Vint v)
        {
            var bytes = Bytes.Empty();
            bytes.Write(new(ZigZag(d.I)));
            bytes.Write(new(t.I));
            bytes.Write(new(ZigZag(v.I)));
            return new(bytes.Trim());
        }

        public DataSeries Add(Date nd, TxId nt, Vint nv)
        {
            var bytes = new Bytes(data);
            int d = (int)UnZigZag(bytes.Read().I);
            UVint t = bytes.Read();
            long v = UnZigZag(bytes.Read().I);
            if (nd.I > d || (nd.I == d && nt.I > t.I))
            {
                var newBytes = Bytes.Empty();
                newBytes.Write(new(ZigZag(nd.I)));
                newBytes.Write(new(nt.I));
                newBytes.Write(new(ZigZag(nv.I)));
                newBytes.Write(new((ulong)(nd.I - d)));
                newBytes.Write(new(nd.I == d ? nt.I - t.I : ZigZag(nt.I - (long)t.I)));
                newBytes.Write(new(ZigZag(nv.I - v)));
                var newData = new byte[data.Length + newBytes.Position - bytes.Position];
                Array.Copy(newBytes.Array, newData, newBytes.Position);
                newBytes.Return();
                if (data.Length > bytes.Position)
                    Array.Copy(data, bytes.Position, newData, newBytes.Position, data.Length - bytes.Position);
                return new(newData);
            }
            while (true)
            {
                if (bytes.Position == data.Length)
                {
                    var newBytes = Bytes.Empty();
                    newBytes.Write(new((ulong)(d - nd.I)));
                    newBytes.Write(new(d == nd.I ? t.I - nt.I : ZigZag((long)t.I - nt.I)));
                    newBytes.Write(new(ZigZag(v - nv.I)));
                    var newData = new byte[bytes.Position + newBytes.Position];
                    Array.Copy(newBytes.Array, 0, newData, bytes.Position, newBytes.Position);
                    newBytes.Return();
                    Array.Copy(data, newData, bytes.Position);
                    return new(newData);
                }
                var i = bytes.Position;
                int cd = d - (int)bytes.Read().I;
                UVint ct = t - (cd == d ? (long)bytes.Read().I : UnZigZag(bytes.Read().I));
                long cv = v - UnZigZag(bytes.Read().I);
                if (nd.I > cd || (nd.I == cd && nt.I > ct.I))
                {
                    var newBytes = Bytes.Empty();
                    newBytes.Write(new((ulong)(d - nd.I)));
                    newBytes.Write(new(d == nd.I ? t.I - nt.I : ZigZag((long)t.I - nt.I)));
                    newBytes.Write(new(ZigZag(v - nv.I)));
                    newBytes.Write(new((ulong)(nd.I - cd)));
                    newBytes.Write(new(nd.I == cd ? nt.I - ct.I : ZigZag(nt.I - (long)ct.I)));
                    newBytes.Write(new(ZigZag(nv.I - cv)));
                    var newData = new byte[newBytes.Position + data.Length + i - bytes.Position];
                    Array.Copy(newBytes.Array, 0, newData, i, newBytes.Position);
                    newBytes.Return();
                    Array.Copy(data, newData, i);
                    Array.Copy(data, bytes.Position, newData, i + newBytes.Position, data.Length - bytes.Position);
                    return new(newData);
                }
                d = cd;
                t = ct;
                v = cv;
            }
        }

        public (Date, TxId, Vint) this[Date queryDate, TxId queryTran]
        {
            get
            {
                var bytes = new Bytes(data);
                int d = (int)UnZigZag(bytes.Read().I);
                UVint t = bytes.Read();
                long v = UnZigZag(bytes.Read().I);
                while (bytes.Position < data.Length && (queryDate.I < d || queryTran.I < t.I))
                {
                    int dd = (int)bytes.Read().I;
                    d -= dd;
                    t -= dd == 0 ? (long)bytes.Read().I : UnZigZag(bytes.Read().I);
                    v -= UnZigZag(bytes.Read().I);
                }
                return (new(d), new((uint)t.I), new(v));
            }
        }

        public (Date, TxId, Vint)[] ToArray()
        {
            var l = new ListSlim<(Date, TxId, Vint)>();
            var bytes = new Bytes(data);
            int d = (int)UnZigZag(bytes.Read().I);
            UVint t = bytes.Read();
            long v = UnZigZag(bytes.Read().I);
            l.Add((new(d), new((uint)t.I), new(v)));
            while (bytes.Position != data.Length)
            {
                int dd = (int)bytes.Read().I;
                d -= dd;
                t -= dd == 0 ? (long)bytes.Read().I : UnZigZag(bytes.Read().I);
                v -= UnZigZag(bytes.Read().I);
                l.Add((new(d), new((uint)t.I), new(v)));
            }
            return l.ToArray();
        }

        IEnumerable<(Date, TxId, Vint)> Enumarable()
        {
            var bytes = new Bytes(data);
            int d = (int)UnZigZag(bytes.Read().I);
            UVint t = bytes.Read();
            long v = UnZigZag(bytes.Read().I);
            yield return (new(d), new((uint)t.I), new(v));
            while (bytes.Position != data.Length)
            {
                int dd = (int)bytes.Read().I;
                d -= dd;
                t -= dd == 0 ? (long)bytes.Read().I : UnZigZag(bytes.Read().I);
                v -= UnZigZag(bytes.Read().I);
                yield return (new(d), new((uint)t.I), new(v));
            }
        }

        public bool After(TxId txId)
        {
            var bytes = new Bytes(data);
            bytes.Read();
            UVint t = bytes.Read();
            if (t.I <= txId.I) return false;
            bytes.Read();
            while (bytes.Position != data.Length)
            {
                t -= bytes.Read().I == 0 ? (long)bytes.Read().I : UnZigZag(bytes.Read().I);
                if (t.I <= txId.I) return false;
                bytes.Read();
            }
            return true;
        }

        public IEnumerator<(Date, TxId, Vint)> GetEnumerator() => Enumarable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Enumarable().GetEnumerator();

        public DataSeries SetAdd(Date nd, TxId nt, Vint nv)
            => data is null ? Single(nd, nt, nv) : Add(nd, nt, nv);
        public DataSeries SetRemove(Date nd, TxId nt, Vint nv)
            => data is null ? Single(nd, nt, new(~nv.I)) : Add(nd, nt, new(~nv.I));

        public IEnumerable<Vint> SetGet(Date nd, TxId nt)
        {
            if (data is not null)
            {
                var removed = new SetSlim<Vint>();
                foreach (var (d, t, v) in Enumarable())
                {
                    if (nd >= d && nt >= t)
                    {
                        if (v.I < 0) removed.Add(new(~v.I));
                        else if (removed.IndexOf(v) < 0) yield return v;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ZigZag(int i) => (uint)((i << 1) ^ (i >> 31));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UnZigZag(uint i) => (int)(i >> 1) ^ -(int)(i & 1u);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ZigZag(long i) => (ulong)((i << 1) ^ (i >> 63));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UnZigZag(ulong i) => (long)(i >> 1) ^ -(long)(i & 1u);
    }
}