using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Net3_Proxy
{
    internal static class Utils
    {
        public static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null)
            {
                return true;
            }
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }
            return true;
        }
        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> seq, T prep)
            => new PrependEnumerable<T>(seq, prep);

        private sealed class PrependEnumerable<T> : IEnumerable<T>
        {
            private readonly IEnumerable<T> rest;
            private readonly T first;

            public PrependEnumerable(IEnumerable<T> rest, T first)
            {
                this.rest = rest;
                this.first = first;
            }

            public IEnumerator<T> GetEnumerator()
            { // TODO: a custom impl that is less garbage
                yield return first;
                foreach (var v in rest) yield return v;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
