using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Net3_Proxy
{

    public class IReadOnlyList<T> : IEnumerable<T>
    {
        private readonly IList<T> list;

        private IReadOnlyList(IList<T> lst)
            => list = lst;

        public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)list).GetEnumerator();

        public int Count => list.Count;

        public T this[int index] => list[index];

        public static implicit operator IReadOnlyList<T>(List<T> list) => new IReadOnlyList<T>(list);
    }

    public class IReadOnlyDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly IDictionary<TKey, TValue> dict;

        private IReadOnlyDictionary(IDictionary<TKey, TValue> d)
            => dict = d;

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => dict.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => dict.GetEnumerator();

        public int Count => dict.Count;

        public bool ContainsKey(TKey key) => dict.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue val)
            => dict.TryGetValue(key, out val);

        public TValue this[TKey key] => dict[key];

        public static implicit operator IReadOnlyDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict)
            => new IReadOnlyDictionary<TKey, TValue>(dict);
    }
}
