using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Net3_Proxy
{
    public static class Tuple
    {
        public static Tuple<T1, T2> Create<T1, T2>(T1 i1, T2 i2)
            => new Tuple<T1, T2>(i1, i2);

        internal static int CombineHashCodes(int h1, int h2)
        {
            return (h1 << 5) + h1 ^ h2;
        }
    }

    [Serializable]
    public class Tuple<T1, T2> : IComparable, IComparable<Tuple<T1, T2>>
    {
        public T1 Item1 { get; private set; }
        public T2 Item2 { get; private set; }

        public Tuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public override bool Equals(object obj)
            => obj is Tuple<T1, T2> tup 
                && Equals(Item1, tup.Item1)
                && Equals(Item2, tup.Item2);

        int IComparable.CompareTo(object obj)
        {
            if (obj == null) return 1;
            var tup = obj as Tuple<T1, T2>;
            if (tup == null) throw new ArgumentException($"Argument must be of type {GetType()}.", "other");
            return CompareTo(tup);
        }

        public override int GetHashCode() => 
            Tuple.CombineHashCodes(EqualityComparer<T1>.Default.GetHashCode(Item1), EqualityComparer<T2>.Default.GetHashCode(Item2));

        public override string ToString() =>
            $"({Item1}, {Item2})";

        public int CompareTo(Tuple<T1, T2> tup)
        {
            int num = Comparer<T1>.Default.Compare(Item1, tup.Item1);
            if (num != 0) return num;
            return Comparer<T2>.Default.Compare(Item2, tup.Item2);
        }

        public static bool operator ==(Tuple<T1, T2> left, Tuple<T1, T2> right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(Tuple<T1, T2> left, Tuple<T1, T2> right)
        {
            return !(left == right);
        }

        public static bool operator <(Tuple<T1, T2> left, Tuple<T1, T2> right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(Tuple<T1, T2> left, Tuple<T1, T2> right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(Tuple<T1, T2> left, Tuple<T1, T2> right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(Tuple<T1, T2> left, Tuple<T1, T2> right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
