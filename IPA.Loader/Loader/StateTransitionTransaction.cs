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
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        public bool WillNeedRestart
            => ThrowIfDisposed<bool>()
            || toEnable.Concat(toDisable).Any(m => m.RuntimeOptions != RuntimeOptions.DynamicInit);

        internal IEnumerable<PluginMetadata> CurrentlyEnabled => currentlyEnabled;
        internal IEnumerable<PluginMetadata> CurrentlyDisabled => currentlyDisabled;
        internal IEnumerable<PluginMetadata> ToEnable => toEnable;
        internal IEnumerable<PluginMetadata> ToDisable => toDisable;

        /// <summary>
        /// Gets a list of plugins that are enabled according to this transaction's current state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        public IEnumerable<PluginMetadata> EnabledPlugins
            => ThrowIfDisposed<IEnumerable<PluginMetadata>>() ?? DisabledPluginsInternal;
        private IEnumerable<PluginMetadata> EnabledPluginsInternal => currentlyEnabled.Except(toDisable).Concat(toEnable);
        /// <summary>
        /// Gets a list of plugins that are disabled according to this transaction's current state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        public IEnumerable<PluginMetadata> DisabledPlugins
            => ThrowIfDisposed<IEnumerable<PluginMetadata>>() ?? DisabledPluginsInternal;
        private IEnumerable<PluginMetadata> DisabledPluginsInternal => currentlyDisabled.Except(toEnable).Concat(toDisable);

        /// <summary>
        /// Checks if a plugin is enabled according to this transaction's current state.
        /// </summary>
        /// <remarks>
        /// <para>This should be roughly equivalent to <c>EnabledPlugins.Contains(meta)</c>, but more performant.</para>
        /// <para>This should also always return the inverse of <see cref="IsDisabled(PluginMetadata)"/> for valid plugins.</para>
        /// </remarks>
        /// <param name="meta">the plugin to check</param>
        /// <returns><see langword="true"/> if the plugin is enabled, <see langword="false"/> otherwise</returns>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <seealso cref="EnabledPlugins"/>
        /// <seealso cref="IsDisabled(PluginMetadata)"/>
        public bool IsEnabled(PluginMetadata meta)
            => ThrowIfDisposed<bool>() || IsEnabledInternal(meta);
        private bool IsEnabledInternal(PluginMetadata meta)
            => (currentlyEnabled.Contains(meta) && !toDisable.Contains(meta))
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
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <seealso cref="DisabledPlugins"/>
        /// <seealso cref="IsEnabled(PluginMetadata)"/>
        public bool IsDisabled(PluginMetadata meta)
            => ThrowIfDisposed<bool>() || IsDisabledInternal(meta);
        private bool IsDisabledInternal(PluginMetadata meta)
            => (currentlyDisabled.Contains(meta) && !toEnable.Contains(meta))
            || toDisable.Contains(meta);

        /// <summary>
        /// Enables a plugin in this transaction.
        /// </summary>
        /// <param name="meta">the plugin to enable</param>
        /// <param name="autoDeps">whether or not to automatically enable all dependencies of the plugin</param>
        /// <returns><see langword="true"/> if the transaction's state was changed, <see langword="false"/> otherwise</returns>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <exception cref="ArgumentException">if <paramref name="meta"/> is not loadable</exception>
        /// <seealso cref="Enable(PluginMetadata, out IEnumerable{PluginMetadata}, bool)"/>
        public bool Enable(PluginMetadata meta, bool autoDeps = true)
            => Enable(meta, out var _, autoDeps);

        /// <summary>
        /// Enables a plugin in this transaction.
        /// </summary>
        /// <remarks>
        /// <paramref name="disabledDeps"/> will only be set when <paramref name="autoDeps"/> is <see langword="false"/>.
        /// </remarks>
        /// <param name="meta">the plugin to enable</param>
        /// <param name="disabledDeps"><see langword="null"/> if successful, otherwise a set of plugins that need to be enabled first</param>
        /// <param name="autoDeps">whether or not to automatically enable all dependencies</param>
        /// <returns><see langword="true"/> if the transaction's state was changed, <see langword="false"/> otherwise</returns>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <exception cref="ArgumentException">if <paramref name="meta"/> is not loadable</exception>
        public bool Enable(PluginMetadata meta, out IEnumerable<PluginMetadata> disabledDeps, bool autoDeps = false)
        { // returns whether or not state was changed
            ThrowIfDisposed();
            if (!currentlyEnabled.Contains(meta) && !currentlyDisabled.Contains(meta))
                throw new ArgumentException(nameof(meta), "Plugin metadata does not represent a loadable plugin");

            disabledDeps = null;
            if (IsEnabledInternal(meta)) return false;

            var needsEnabled = meta.Dependencies.Where(m => DisabledPluginsInternal.Contains(m));
            if (autoDeps)
            {
                foreach (var dep in needsEnabled)
                {
                    var res = Enable(dep, out var failedDisabled, true);
                    if (failedDisabled == null) continue;
                    disabledDeps = failedDisabled;
                    return res;
                }
            }
            else if (needsEnabled.Any())
            {
                // there are currently enabled plugins that depend on this
                disabledDeps = needsEnabled;
                return false;
            }

            toDisable.Remove(meta);
            toEnable.Add(meta);
            return true;
        }

        /// <summary>
        /// Disables a plugin in this transaction.
        /// </summary>
        /// <param name="meta">the plugin to disable</param>
        /// <param name="autoDependents">whether or not to automatically disable all dependents of the plugin</param>
        /// <returns><see langword="true"/> if the transaction's state was changed, <see langword="false"/> otherwise</returns>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <exception cref="ArgumentException">if <paramref name="meta"/> is not loadable</exception>
        /// <seealso cref="Disable(PluginMetadata, out IEnumerable{PluginMetadata}, bool)"/>
        public bool Disable(PluginMetadata meta, bool autoDependents = true)
            => Disable(meta, out var _, autoDependents);

        /// <summary>
        /// Disables a plugin in this transaction.
        /// </summary>
        /// <remarks>
        /// <paramref name="enabledDependents"/> will only be set when <paramref name="autoDependents"/> is <see langword="false"/>.
        /// </remarks>
        /// <param name="meta">the plugin to disable</param>
        /// <param name="enabledDependents"><see langword="null"/> if successful, otherwise a set of plugins that need to be disabled first</param>
        /// <param name="autoDependents">whether or not to automatically disable all dependents of the plugin</param>
        /// <returns><see langword="true"/> if the transaction's state was changed, <see langword="false"/> otherwise</returns>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <exception cref="ArgumentException">if <paramref name="meta"/> is not loadable</exception>
        public bool Disable(PluginMetadata meta, out IEnumerable<PluginMetadata> enabledDependents, bool autoDependents = false)
        { // returns whether or not state was changed
            ThrowIfDisposed();
            if (!currentlyEnabled.Contains(meta) && !currentlyDisabled.Contains(meta))
                throw new ArgumentException(nameof(meta), "Plugin metadata does not represent a loadable plugin");

            enabledDependents = null;
            if (IsDisabledInternal(meta)) return false;

            var needsDisabled = EnabledPluginsInternal.Where(m => m.Dependencies.Contains(meta));
            if (autoDependents)
            {
                foreach (var dep in needsDisabled)
                {
                    var res = Disable(dep, out var failedEnabled, true);
                    if (failedEnabled == null) continue;
                    enabledDependents = failedEnabled;
                    return res;
                }
            }
            else if (needsDisabled.Any())
            {
                // there are currently enabled plugins that depend on this
                enabledDependents = needsDisabled;
                return false;
            }

            toDisable.Add(meta);
            toEnable.Remove(meta);
            return true;
        }

        /// <summary>
        /// Commits this transaction to actual state, enabling and disabling plugins as necessary.
        /// </summary>
        /// <remarks>
        /// <para>After this completes, this transaction will be disposed.</para>
        /// <para>
        /// The <see cref="Task"/> that is returned will error if <b>any</b> of the mods being <b>disabled</b>
        /// error. It is up to the caller to handle these in a sane way, like logging them. If nothing else, do something like this:
        /// <code lang="csharp">
        /// // get your transaction...
        /// var complete = transaction.Commit();
        /// await complete.ContinueWith(t => {
        ///     if (t.IsFaulted)
        ///         Logger.log.Error($"Error disabling plugins: {t.Exception}");
        /// });
        /// </code>
        /// If you are running in a coroutine, you can use <see cref="Utilities.Async.Coroutines.WaitForTask(Task)"/> instead of <see langword="await"/>.
        /// </para>
        /// <para>
        /// If you are running on the Unity main thread, this will block until all enabling is done, and will return a task representing the disables.
        /// Otherwise, the task returned represents both, and <i>will not complete</i> until Unity has done (possibly) several updates, depending on
        /// the number of plugins being disabled, and the time they take.
        /// </para>
        /// </remarks>
        /// <returns>a <see cref="Task"/> which completes whenever all disables complete</returns>
        /// <exception cref="ObjectDisposedException">if this object has been disposed</exception>
        /// <exception cref="InvalidOperationException">if the plugins' state no longer matches this transaction's original state</exception>
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
