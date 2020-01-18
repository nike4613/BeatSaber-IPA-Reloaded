using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Runtime.CompilerServices
{
    public sealed class ConditionalWeakTable<TKey, TValue> where TKey : class where TValue : class
    {
        private readonly Dictionary<WeakReference<TKey>, TValue> items = new Dictionary<WeakReference<TKey>, TValue>();
        private readonly object _lock = new object();

        private sealed class KeyComparer : IEqualityComparer<WeakReference<TKey>>
        {
            public bool Equals(WeakReference<TKey> x, WeakReference<TKey> y)
                => x.TryGetTarget(out var keyX) && y.TryGetTarget(out var keyY) && ReferenceEquals(keyX, keyY);

            public int GetHashCode(WeakReference<TKey> obj)
                => obj.TryGetTarget(out var key) ? key.GetHashCode() : 0;
        }

        private static WeakReference<TKey> WeakRef(TKey key)
            => new WeakReference<TKey>(key);

        private sealed class GCTracker
        {
            public static event Action OnGC;
            private static readonly WeakReference<GCTracker> tracker = new WeakReference<GCTracker>(new GCTracker());
            ~GCTracker()
            {
                OnGC?.Invoke();
                if (!AppDomain.CurrentDomain.IsFinalizingForUnload() && !Environment.HasShutdownStarted)
                    tracker.SetTarget(new GCTracker());
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentException("Null key", nameof(key));
            lock (_lock)
                items.Add(WeakRef(key), value);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
                throw new ArgumentException("Null key", nameof(key));

            value = null;
            lock (_lock)
                return items.TryGetValue(WeakRef(key), out value);
        }

        public delegate TValue CreateValueCallback(TKey key);

        public TValue GetValue(TKey key, CreateValueCallback createValueCallback)
        {
            if (createValueCallback == null)
                throw new ArgumentException("Null create delegate", nameof(createValueCallback));

            lock (_lock)
            {
                if (TryGetValue(key, out var value))
                    return value;
                else
                {
                    value = createValueCallback(key);
                    Add(key, value);
                    return value;
                }
            }
        }

        public TValue GetOrCreateValue(TKey key)
            => GetValue(key, k => Activator.CreateInstance<TValue>());

        public bool Remove(TKey key)
        {
            if (key == null)
                throw new ArgumentException("Null key", nameof(key));

            return items.Remove(WeakRef(key));
        }

        public ConditionalWeakTable()
            => GCTracker.OnGC += OnGC;
        ~ConditionalWeakTable()
            => GCTracker.OnGC -= OnGC;

        private void OnGC()
        {
            // on each GC, we want to clear the entire set of empty keys
            var nullWeakRef = WeakRef(null);
            while (items.Remove(nullWeakRef)) ; // just loop
        }
    }
}
