using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Loader
{
    public sealed class StateTransitionTransaction : IDisposable
    {
        private readonly HashSet<PluginMetadata> currentlyEnabled;
        private readonly HashSet<PluginMetadata> currentlyDisabled;
        private readonly HashSet<PluginMetadata> toEnable = new HashSet<PluginMetadata>();
        private readonly HashSet<PluginMetadata> toDisable = new HashSet<PluginMetadata>();

        internal StateTransitionTransaction(IEnumerable<PluginMetadata> enabled, IEnumerable<PluginMetadata> disabled)
        {
            currentlyEnabled = new HashSet<PluginMetadata>(enabled.ToArray());
            currentlyDisabled = new HashSet<PluginMetadata>(disabled.ToArray());
        }

        public bool WillNeedRestart => toEnable.Concat(toDisable).Any(m => m.RuntimeOptions != RuntimeOptions.DynamicInit);

        internal IEnumerable<PluginMetadata> ToEnable => toEnable;
        internal IEnumerable<PluginMetadata> ToDisable => toDisable;

        public IEnumerable<PluginMetadata> EnabledPlugins => currentlyEnabled.Except(toDisable).Concat(toEnable);
        public IEnumerable<PluginMetadata> DisabledPlugins => currentlyDisabled.Except(toEnable).Concat(toDisable);

        public bool IsEnabled(PluginMetadata meta)
            => ThrowIfDisposed<bool>()
            || (currentlyEnabled.Contains(meta) && !toDisable.Contains(meta))
            || toEnable.Contains(meta);
        public bool IsDisabled(PluginMetadata meta)
            => ThrowIfDisposed<bool>()
            || (currentlyDisabled.Contains(meta) && !toEnable.Contains(meta))
            || toDisable.Contains(meta);

        public bool Enable(PluginMetadata meta)
        { // returns whether or not state was changed
            ThrowIfDisposed();
            if (!currentlyEnabled.Contains(meta) && !currentlyDisabled.Contains(meta))
                throw new ArgumentException(nameof(meta), "Plugin metadata does not represent a loadable plugin");

            if (toEnable.Contains(meta)) return false;
            if (currentlyEnabled.Contains(meta) && !toDisable.Contains(meta)) return false;
            toDisable.Remove(meta);
            toEnable.Add(meta);
            return true;
        }

        public bool Disable(PluginMetadata meta)
        { // returns whether or not state was changed
            ThrowIfDisposed();
            if (!currentlyEnabled.Contains(meta) && !currentlyDisabled.Contains(meta))
                throw new ArgumentException(nameof(meta), "Plugin metadata does not represent a ");

            if (toEnable.Contains(meta)) return false;
            if (currentlyEnabled.Contains(meta) && !toDisable.Contains(meta)) return false;
            toDisable.Remove(meta);
            toEnable.Add(meta);
            return true;
        }

        public Task Commit() => PluginManager.CommitTransaction(this);

        private void ThrowIfDisposed() => ThrowIfDisposed<byte>();
        private T ThrowIfDisposed<T>()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StateTransitionTransaction));
            return default;
        }

        private bool disposed = false;
        public void Dispose()
            => disposed = true;
    }
}
