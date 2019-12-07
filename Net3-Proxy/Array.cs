using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OgArray = System.Array;

namespace Net3_Proxy
{
    public static class Array
    {
        private static class EmptyArray<T>
        {
            public static readonly T[] Value = new T[0];
        }

        public static T[] Empty<T>() => EmptyArray<T>.Value;

    }
}
