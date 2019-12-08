using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IPA.Config.Data
{
    /// <summary>
    /// A list of <see cref="Value"/>s for serialization by an <see cref="IConfigProvider"/>.
    /// Use <see cref="Value.List"/> or <see cref="Value.From(IEnumerable{Value})"/> to create.
    /// </summary>
    public sealed class List : Value, IList<Value>
    {
        private readonly List<Value> values = new List<Value>();

        internal List() { }

        /// <summary>
        /// Gets the value at the given index in this <see cref="List"/>.
        /// </summary>
        /// <param name="index">the index to retrieve the <see cref="Value"/> at</param>
        /// <returns>the <see cref="Value"/> at <paramref name="index"/></returns>
        /// <seealso cref="IList{T}.this[int]"/>
        public Value this[int index] { get => values[index]; set => values[index] = value; }

        /// <summary>
        /// Gets the number of elements in the <see cref="List"/>.
        /// </summary>
        /// <seealso cref="ICollection{T}.Count"/>
        public int Count => values.Count;

        bool ICollection<Value>.IsReadOnly => ((IList<Value>)values).IsReadOnly;

        /// <summary>
        /// Adds a <see cref="Value"/> to the end of this <see cref="List"/>.
        /// </summary>
        /// <param name="item">the <see cref="Value"/> to add</param>
        /// <seealso cref="ICollection{T}.Add(T)"/>
        public void Add(Value item) => values.Add(item);

        /// <summary>
        /// Adds a range of <see cref="Value"/>s to the end of this <see cref="List"/>.
        /// </summary>
        /// <param name="vals">the range of <see cref="Value"/>s to add</param>
        public void AddRange(IEnumerable<Value> vals)
        {
            foreach (var val in vals) Add(val);
        }

        /// <summary>
        /// Clears the <see cref="List"/>.
        /// </summary>
        /// <seealso cref="ICollection{T}.Clear"/>
        public void Clear() => values.Clear();

        /// <summary>
        /// Checks if the <see cref="List"/> contains a certian item.
        /// </summary>
        /// <param name="item">the <see cref="Value"/> to check for</param>
        /// <returns><see langword="true"/> if the item was founc, otherwise <see langword="false"/></returns>
        /// <seealso cref="ICollection{T}.Contains(T)"/>
        public bool Contains(Value item) => values.Contains(item);

        /// <summary>
        /// Copies the <see cref="Value"/>s in the <see cref="List"/> to the <see cref="Array"/> in <paramref name="array"/>.
        /// </summary>
        /// <param name="array">the <see cref="Array"/> to copy to</param>
        /// <param name="arrayIndex">the starting index to copy to</param>
        /// <seealso cref="ICollection{T}.CopyTo(T[], int)"/>
        public void CopyTo(Value[] array, int arrayIndex) => values.CopyTo(array, arrayIndex);

        /// <summary>
        /// Gets an enumerator to enumerate the <see cref="List"/>.
        /// </summary>
        /// <returns>an <see cref="IEnumerator{T}"/> for this <see cref="List"/></returns>
        /// <seealso cref="IEnumerable{T}.GetEnumerator"/>
        public IEnumerator<Value> GetEnumerator() => ((IList<Value>)values).GetEnumerator();

        /// <summary>
        /// Gets the index that a given <see cref="Value"/> is in the <see cref="List"/>.
        /// </summary>
        /// <param name="item">the <see cref="Value"/> to search for</param>
        /// <returns>the index that the <paramref name="item"/> was at, or -1.</returns>
        /// <seealso cref="IList{T}.IndexOf(T)"/>
        public int IndexOf(Value item) => values.IndexOf(item);

        /// <summary>
        /// Inserts a <see cref="Value"/> at an index.
        /// </summary>
        /// <param name="index">the index to insert at</param>
        /// <param name="item">the <see cref="Value"/> to insert</param>
        /// <seealso cref="IList{T}.Insert(int, T)"/>
        public void Insert(int index, Value item) => values.Insert(index, item);

        /// <summary>
        /// Removes a <see cref="Value"/> from the <see cref="List"/>.
        /// </summary>
        /// <param name="item">the <see cref="Value"/> to remove</param>
        /// <returns><see langword="true"/> if the item was removed, <see langword="false"/> otherwise</returns>
        /// <seealso cref="ICollection{T}.Remove(T)"/>
        public bool Remove(Value item) => values.Remove(item);

        /// <summary>
        /// Removes a <see cref="Value"/> at an index.
        /// </summary>
        /// <param name="index">the index to remove a <see cref="Value"/> at</param>
        /// <seealso cref="IList{T}.RemoveAt(int)"/>
        public void RemoveAt(int index) => values.RemoveAt(index);

        /// <summary>
        /// Converts this <see cref="Value"/> into a human-readable format.
        /// </summary>
        /// <returns>a comma-seperated list of the result of <see cref="Value.ToString"/> wrapped in square brackets</returns>
        public override string ToString()
            => $"[{string.Join(",",this.Select(v => v?.ToString() ?? "null").StrJP())}]";

        IEnumerator IEnumerable.GetEnumerator() => ((IList<Value>)values).GetEnumerator();
    }


}
