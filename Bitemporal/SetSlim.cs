using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bitemporal
{
    public class SetSlim<T> : IReadOnlyCollection<T> where T : IEquatable<T>
    {
        struct Entry { internal int Bucket; internal int Next; internal T Item; }
        static class Holder { internal static Entry[] Initial = new Entry[1]; }
        int count;
        Entry[] entries;
        public SetSlim() => entries = Holder.Initial;

        public SetSlim(int capacity)
        {
            if (capacity < 2) capacity = 2;
            entries = new Entry[PowerOf2(capacity)];
        }

        public SetSlim(IEnumerable<T> items)
        {
            entries = new Entry[2];
            foreach (var i in items) Add(i);
        }

        static int PowerOf2(int capacity)
        {
            if ((capacity & (capacity - 1)) == 0) return capacity;
            int i = 2;
            while (i < capacity) i <<= 1;
            return i;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        Entry[] Resize()
        {
            if (entries.Length == 1) return entries = new Entry[2];
            var oldEntries = entries;
            var newEntries = new Entry[oldEntries.Length * 2];
            for (int i = 0; i < oldEntries.Length;)
            {
                var bucketIndex = oldEntries[i].Item.GetHashCode() & (newEntries.Length - 1);
                newEntries[i].Next = newEntries[bucketIndex].Bucket - 1;
                newEntries[i].Item = oldEntries[i].Item;
                newEntries[bucketIndex].Bucket = ++i;
            }
            return entries = newEntries;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        int AddItem(T item, int hashCode)
        {
            var i = count;
            var ent = entries;
            if (ent.Length == i || ent.Length == 1) ent = Resize();
            var bucketIndex = hashCode & (ent.Length - 1);
            ent[i].Next = ent[bucketIndex].Bucket - 1;
            ent[i].Item = item;
            ent[bucketIndex].Bucket = ++count;
            return i;
        }

        public int Add(T item)
        {
            var ent = entries;
            var hashCode = item.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !item.Equals(ent[i].Item)) i = ent[i].Next;
            return i >= 0 ? i : AddItem(item, hashCode);
        }

        public int IndexOf(T item)
        {
            var ent = entries;
            var hashCode = item.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !item.Equals(ent[i].Item)) i = ent[i].Next;
            return i;
        }

        public bool Contains(T item)
        {
            var ent = entries;
            var hashCode = item.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !item.Equals(ent[i].Item)) i = ent[i].Next;
            return i >= 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
                yield return entries[i].Item;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T this[int i] => entries[i].Item;
        public int Count => count;
    }
}