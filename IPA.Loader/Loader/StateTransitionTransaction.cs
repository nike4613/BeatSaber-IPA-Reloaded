using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Loader
{
    /// <summary>
    /// A class to represent a transaction for changing the state of loaded mods.
    /// </summary>
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

        /// <summary>
        /// Gets whether or not a game restart will be necessary to fully apply this transaction.
        /// </summary>
        /// <value><see langword="true"/> if any mod who's state is changed cannot be changed at runtime, <see langword="false"/> otherwise</value>
        public bool WillNeedRestart => toEnable.Concat(toDisable).Any(m => m.RuntimeOptions != RuntimeOptions.DynamicInit);

        internal IEnumerable<PluginMetadata> ToEnable => toEnable;
        internal IEnumerable<PluginMetadata> ToDisable => toDisable;

        /// <summary>
        /// Gets a list of plugins that are enabled according to this transaction's current state.
        /// </summary>
        public IEnumerable<PluginMetadata> EnabledPlugins 
            => ThrowIfDisposed<IEnumerable<PluginMetadata>>() 
            ?? currentlyEnabled.Except(toDisable).Concat(toEnable);
        /// <summary>
        /// Gets a list of plugins that are disabled according to this transaction's current state.
        /// </summary>
        public IEnumerable<PluginMetadata> DisabledPlugins 
            => ThrowIfDisposed<IEnumerable<PluginMetadata>>()
            ?? currentlyDisabled.Except(toEnable).Concat(toDisable);

        /// <summary>
        /// Checks if a plugin is enabled according to this transaction's current state.
        /// </summary>
        /// <remarks>
        /// <para>This should be roughly equivalent to <c>EnabledPlugins.Contains(meta)</c>, but more performant.</para>
        /// <para>This should also always return the inverse of <see cref="IsDisabled(PluginMetadata)"/> for valid plugins.</para>
        /// </remarks>
        /// <param name="meta">the plugin to check</param>
        /// <returns><see langword="true"/> if the plugin is enabled, <see langword="false"/> otherwise</returns>
        /// <seealso cref="EnabledPlugins"/>
        /// <see cref="IsDisabled(PluginMetadata)"/>
        public bool IsEnabled(PluginMetadata meta)
            => ThrowIfDisposed<bool>()
            || (currentlyEnabled.Contains(meta) && !toDisable.Contains(meta))
            || toEnable.Contains(meta);
        /// <summary>
        /// Checks if a plugin is disabled according to this transaction's current state.
        /// </summary>
        /// <remarks>
        /// <para>This should be roughly equivalent to <c>DisabledPlugins.Contains(meta)</c>, but more performant.</para>
        /// <para>This should also always return the inverse of <see cref="IsEnabled(PluginMetadata)"/> for valid plugins.</para>
        /// </remarks>
        /// <param name="meta">the plugin to check</param>
        /// <returns><see langword="true"/> if the plugin is disabled, <see langword="false"/> otherwise</returns>
        /// <seealso cref="DisabledPlugins"/>
        /// <see cref="IsEnabled(PluginMetadata)"/>
        public bool IsDisabled(PluginMetadata meta)
            => ThrowIfDisposed<bool>()
            || (currentlyDisabled.Contains(meta) && !toEnable.Contains(meta))
            || toDisable.Contains(meta);

        /// <summary>
        /// Enables a plugin in this transaction.
        /// </summary>
        /// <param name="meta">the plugin to enable</param>
        /// <returns><see langword="true"/> if the transaction's state was changed, <see langword="false"/> otherwise</returns>
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

        /// <summary>
        /// Disables a plugin in this transaction.
        /// </summary>
        /// <param name="meta">the plugin to disable</param>
        /// <returns><see langword="true"/> if the transaction's state was changed, <see langword="false"/> otherwise</returns>
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

        /// <summary>
        /// Commits this transaction to actual state, enabling and disabling plugins as necessary.
        /// </summary>
        /// <returns>a <see cref="Task"/> which completes whenever all disables complete</returns>
        public Task Commit() => ThrowIfDisposed<Task>() ?? PluginManager.CommitTransaction(this);

        private void ThrowIfDisposed() => ThrowIfDisposed<byte>();
        private T ThrowIfDisposed<T>()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StateTransitionTransaction));
            return default;
        }

        private bool disposed = false;
        /// <summary>
        /// Disposes and discards this transaction without committing it.
        /// </summary>
        public void Dispose()
            => disposed = true;
    }
}
