using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Utilities.Async
{
    /// <summary>
    /// A dictionary-like type intended for thread-safe value caches whose values are created only once ever.
    /// </summary>
    /// <typeparam name="TKey">the key type of the cache</typeparam>
    /// <typeparam name="TValue">the value type of the cache</typeparam>
    /// <remarks>
    /// This object basically wraps a <see cref="ConcurrentDictionary{TKey, TValue}"/> with some special handling
    /// to ensure that values are only created once ever, without having multiple parallel constructions.
    /// </remarks>
    public class SingleCreationValueCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, (ManualResetEventSlim wh, TValue val)> dict;

        private static KeyValuePair<TKey, (ManualResetEventSlim, TValue)> ExpandKeyValuePair(KeyValuePair<TKey, TValue> kvp)
            => new KeyValuePair<TKey, (ManualResetEventSlim, TValue)>(kvp.Key, (null, kvp.Value));
        private static KeyValuePair<TKey, TValue> CompressKeyValuePair(KeyValuePair<TKey, (ManualResetEventSlim, TValue value)> kvp)
            => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.value);

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleCreationValueCache{TKey, TValue}"/>
        /// class that is empty, has the default concurrency level, has the default initial
        /// capacity, and uses the default comparer for the key type.
        /// </summary>
        public SingleCreationValueCache()
            => dict = new ConcurrentDictionary<TKey, (ManualResetEventSlim wh, TValue val)>();
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleCreationValueCache{TKey, TValue}"/>
        /// class that contains elements copied from the specified <see cref="IEnumerable{T}"/>,
        /// has the default concurrency level, has the default initial capacity, and uses
        /// the default comparer for the key type.
        /// </summary>
        /// <param name="collection">the <see cref="IEnumerable{T}"/> whose element are to be used for the new cache</param>
        /// <exception cref="ArgumentNullException">when any arguments are null</exception>
        /// <exception cref="ArgumentException"><paramref name="collection"/> contains duplicate keys</exception>
        public SingleCreationValueCache(IEnumerable<KeyValuePair<TKey, TValue>> collection)
            => dict = new ConcurrentDictionary<TKey, (ManualResetEventSlim wh, TValue val)>(collection.Select(ExpandKeyValuePair));
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleCreationValueCache{TKey, TValue}"/>
        /// class that is empty, has the default concurrency level and capacity, and uses
        /// the specified <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="comparer">the equality comparer to use when comparing keys</param>
        /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null</exception>
        public SingleCreationValueCache(IEqualityComparer<TKey> comparer)
            => dict = new ConcurrentDictionary<TKey, (ManualResetEventSlim wh, TValue val)>(comparer);
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleCreationValueCache{TKey, TValue}"/>
        /// class that contains elements copied from the specified <see cref="IEnumerable{T}"/>
        /// has the default concurrency level, has the default initial capacity, and uses
        /// the specified <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="collection">the <see cref="IEnumerable{T}"/> whose elements are to be used for the new cache</param>
        /// <param name="comparer">the equality comparer to use when comparing keys</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> or <paramref name="comparer"/> is null</exception>
        public SingleCreationValueCache(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
            => dict = new ConcurrentDictionary<TKey, (ManualResetEventSlim wh, TValue val)>(collection.Select(ExpandKeyValuePair), comparer);
        #endregion

        /// <summary>
        /// Gets a value that indicates whether this cache is empty. 
        /// </summary>
        public bool IsEmpty => dict.IsEmpty;
        /// <summary>
        /// Gets the number of elements that this cache contains.
        /// </summary>
        public int Count => dict.Count;

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear() => dict.Clear();
        /// <summary>
        /// Gets a value indicating whether or not this cache contains <paramref name="key"/>.
        /// </summary>
        /// <param name="key">the key to search for</param>
        /// <returns><see langword="true"/> if the cache contains the key, <see langword="false"/> otherwise</returns>
        public bool ContainsKey(TKey key) => dict.ContainsKey(key);
        /// <summary>
        /// Copies the key-value pairs stored by the cache to a new array, filtering all elements that are currently being
        /// created.
        /// </summary>
        /// <returns>an array containing a snapshot of the key-value pairs contained in this cache</returns>
        public KeyValuePair<TKey, TValue>[] ToArray()
            => dict.ToArray().Where(k => k.Value.wh == null).Select(CompressKeyValuePair).ToArray();

        /// <summary>
        /// Attempts to get the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="key">the key to search for</param>
        /// <param name="value">the value retrieved, if any</param>
        /// <returns><see langword="true"/> if the value was found, <see langword="false"/> otherwise</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out var pair) && pair.wh != null)
            {
                value = pair.val;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Gets the value associated with the specified key from the cache. If it does not exist, and
        /// no creators are currently running for this key, then the creator is called to create the value
        /// and the value is added to the cache. If there is a creator currently running for the key, then
        /// this waits for the creator to finish and retrieves the value.
        /// </summary>
        /// <param name="key">the key to search for</param>
        /// <param name="creator">the delegate to use to create the value if it does not exist</param>
        /// <returns>the value that was found, or the result of <paramref name="creator"/></returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> creator)
        {
        retry:
            if (dict.TryGetValue(key, out var value))
            {
                if (value.wh != null)
                {
                    value.wh.Wait();
                    goto retry; // this isn't really a good candidate for a loop
                    // the loop condition will never be hit, and this should only
                    //   jump back to the beginning in exceptional situations
                }
                return value.val;
            }
            else
            {
                var wh = new ManualResetEventSlim(false);
                var cmp = (wh, default(TValue));
                if (!dict.TryAdd(key, cmp))
                    goto retry; // someone else beat us to the punch, retry getting their value and wait for them
                var val = creator(key);
                while (!dict.TryUpdate(key, (null, val), cmp))
                    throw new InvalidOperationException();
                wh.Set();
                return val;
            }
        }
    }
}
