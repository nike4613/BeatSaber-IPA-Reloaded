using System.Collections;
using System.Collections.Generic;

namespace IPA.Config.Data
{

    /// <summary>
    /// A map of <see cref="string"/> to <see cref="Value"/> for serialization by an <see cref="IConfigProvider"/>.
    /// </summary>
    public sealed class Map : Value, IDictionary<string, Value>
    {
        private readonly Dictionary<string, Value> values = new Dictionary<string, Value>();

        /// <summary>
        /// Accesses the <see cref="Value"/> at <paramref name="key"/> in the map.
        /// </summary>
        /// <param name="key">the key to get the value associated with</param>
        /// <returns>the value associated with the <paramref name="key"/></returns>
        /// <seealso cref="IDictionary{TKey, TValue}.this[TKey]"/>
        public Value this[string key] { get => values[key]; set => values[key] = value; }

        /// <summary>
        /// Gets a collection of the keys for the <see cref="Map"/>.
        /// </summary>
        /// <seealso cref="IDictionary{TKey, TValue}.Keys"/>
        public ICollection<string> Keys => values.Keys;

        /// <summary>
        /// Gets a collection of the values in the <see cref="Map"/>.
        /// </summary>
        /// <seealso cref="IDictionary{TKey, TValue}.Values"/>
        public ICollection<Value> Values => values.Values;

        /// <summary>
        /// Gets the number of key-value pairs in this <see cref="Map"/>.
        /// </summary>
        /// <seealso cref="ICollection{T}.Count"/>
        public int Count => values.Count;

        bool ICollection<KeyValuePair<string, Value>>.IsReadOnly => ((IDictionary<string, Value>)values).IsReadOnly;

        /// <summary>
        /// Adds a new <see cref="Value"/> with a given key.
        /// </summary>
        /// <param name="key">the key to put the value at</param>
        /// <param name="value">the <see cref="Value"/> to add</param>
        /// <seealso cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/>
        public void Add(string key, Value value) => values.Add(key, value);

        void ICollection<KeyValuePair<string, Value>>.Add(KeyValuePair<string, Value> item) 
            => ((IDictionary<string, Value>)values).Add(item);

        /// <summary>
        /// Clears the <see cref="Map"/> of its key-value pairs.
        /// </summary>
        /// <seealso cref="ICollection{T}.Clear"/>
        public void Clear() => values.Clear();

        bool ICollection<KeyValuePair<string, Value>>.Contains(KeyValuePair<string, Value> item) 
            => ((IDictionary<string, Value>)values).Contains(item);

        /// <summary>
        /// Checks if the <see cref="Map"/> contains a given <paramref name="key"/>.
        /// </summary>
        /// <param name="key">the key to check for</param>
        /// <returns><see langword="true"/> if the key exists, otherwise <see langword="false"/></returns>
        /// <seealso cref="IDictionary{TKey, TValue}.ContainsKey(TKey)"/>
        public bool ContainsKey(string key) => values.ContainsKey(key);

        void ICollection<KeyValuePair<string, Value>>.CopyTo(KeyValuePair<string, Value>[] array, int arrayIndex)
            => ((IDictionary<string, Value>)values).CopyTo(array, arrayIndex);

        /// <summary>
        /// Enumerates the <see cref="Map"/>'s key-value pairs.
        /// </summary>
        /// <returns>an <see cref="IEnumerator{T}"/> of key-value pairs in this <see cref="Map"/></returns>
        /// <seealso cref="IEnumerable{T}.GetEnumerator()"/>
        public IEnumerator<KeyValuePair<string, Value>> GetEnumerator() => values.GetEnumerator();

        /// <summary>
        /// Removes the object associated with a key in this <see cref="Map"/>.
        /// </summary>
        /// <param name="key">the key to remove</param>
        /// <returns><see langword="true"/> if the key existed, <see langword="false"/> otherwise</returns>
        /// <seealso cref="IDictionary{TKey, TValue}.Remove(TKey)"/>
        public bool Remove(string key) => values.Remove(key);

        bool ICollection<KeyValuePair<string, Value>>.Remove(KeyValuePair<string, Value> item) 
            => ((IDictionary<string, Value>)values).Remove(item);

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">the key of the value to get</param>
        /// <param name="value">the target location of the retrieved object</param>
        /// <returns><see langword="true"/> if the key was found and <paramref name="value"/> set, <see langword="false"/> otherwise</returns>
        /// <seealso cref="IDictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
        public bool TryGetValue(string key, out Value value) => values.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<string, Value>)values).GetEnumerator();
    }


}
