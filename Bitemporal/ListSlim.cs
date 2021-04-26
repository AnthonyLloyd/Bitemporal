using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bitemporal
{
    public class ListSlim<T> : IReadOnlyList<T>
    {
        static class Holder { internal static T[] Initial = Array.Empty<T>(); }
        T[] entries;
        int count;
        public ListSlim() => entries = Holder.Initial;
        public ListSlim(int capacity) => entries = new T[capacity];
        public ListSlim(IEnumerable<T> items)
        {
            if (items is ICollection<T> ts)
            {
                entries = new T[ts.Count];
                ts.CopyTo(entries, 0);
            }
            else entries = items.ToArray();
            count = entries.Length;
        }
        public int Count => count;
        public T this[int i]
        {
            get => entries[i];
            set => entries[i] = value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void AddWithResize(T item)
        {
            if (count == 0)
            {
                entries = new T[2];
                entries[0] = item;
                count = 1;
            }
            else
            {
                var newEntries = new T[count * 2];
                Array.Copy(entries, 0, newEntries, 0, count);
                newEntries[count] = item;
                entries = newEntries;
                count++;
            }
        }

        public void Add(T item)
        {
            T[] e = entries;
            int c = count;
            if ((uint)c < (uint)e.Length)
            {
                e[c] = item;
                count = c + 1;
            }
            else AddWithResize(item);
        }

        public T[] ToArray()
        {
            int c = count;
            var a = new T[c];
            Array.Copy(entries, 0, a, 0, c);
            return a;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
                yield return entries[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}