using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Net3_Proxy
{

    public class IReadOnlyList<T> : IEnumerable<T>
    {
        private IList<T> list;

        private IReadOnlyList(IList<T> lst)
        {
            list = lst;
        }

        public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)list).GetEnumerator();

        public int Count => list.Count;

        public T this[int index] => list[index];

        public static implicit operator IReadOnlyList<T>(List<T> list) => new IReadOnlyList<T>(list);
    }
}
