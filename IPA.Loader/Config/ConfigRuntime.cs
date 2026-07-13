using IPA.Utilities.Async;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger = IPA.Logging.Logger;

namespace IPA.Config
{
    internal static class ConfigRuntime
    {
        private static readonly ConcurrentBag<Config> configs = new();
        private static readonly AutoResetEvent configsChangedWatcher = new(false);
        private static readonly TimeSpan watchInterval = TimeSpan.FromSeconds(1);
        private static volatile Config[] watchedConfigs = Array.Empty<Config>();
        private static BlockingCollection<IConfigStore> requiresSave = new();
        private static SingleThreadTaskScheduler loadScheduler;
        private static TaskFactory loadFactory;
        private static Thread saveThread;
        private static Thread legacySaveThread;
        private static Thread watchThread;

        private static void TryStartRuntime()
        {
            if (loadScheduler == null || !loadScheduler.IsRunning)
            {
                loadFactory = null;
                loadScheduler = new SingleThreadTaskScheduler();
                loadScheduler.Start();
            }
            if (loadFactory == null)
                loadFactory = new TaskFactory(loadScheduler);
            if (saveThread == null || !saveThread.IsAlive)
            {
                saveThread = new Thread(SaveThread);
                saveThread.Start();
            }
            if (legacySaveThread == null || !legacySaveThread.IsAlive)
            {
                legacySaveThread = new Thread(LegacySaveThread);
                legacySaveThread.Start();
            }
            if (watchThread == null || !watchThread.IsAlive)
            {
                watchThread = new Thread(WatchThread) { IsBackground = true };
                watchThread.Start();
            }

            AppDomain.CurrentDomain.ProcessExit -= ShutdownRuntime;
            AppDomain.CurrentDomain.ProcessExit += ShutdownRuntime;
        }

        internal static void AddRequiresSave(IConfigStore configStore)
        {
            requiresSave?.Add(configStore);
        }

        private static void ShutdownRuntime(object sender, EventArgs e)
            => ShutdownRuntime();
        internal static void ShutdownRuntime()
        {
            try
            {
                watchThread.Abort();
                loadScheduler.Join(); // we can wait for the loads to finish
                saveThread.Abort(); // eww, but i don't like any of the other potential solutions
                legacySaveThread.Abort();

                SaveAll();

                requiresSave?.Dispose();
                requiresSave = null;
            }
            catch
            {
            }
        }

        public static void RegisterConfig(Config cfg)
        {
            lock (configs)
            { // we only lock this segment, so that this only waits on other calls to this
                if (configs.ToArray().Contains(cfg))
                    throw new InvalidOperationException("Config already registered to runtime!");

                cfg.File.Refresh();
                cfg.LastKnownWriteTimeUtc = cfg.File.Exists ? cfg.File.LastWriteTimeUtc : DateTime.MinValue;

                configs.Add(cfg);
                watchedConfigs = configs.ToArray();
            }
            configsChangedWatcher.Set();

            TryStartRuntime();
        }

        public static void ConfigChanged()
        {
            configsChangedWatcher.Set();
        }

        private static void WatchThread()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(watchInterval);

                    var watched = watchedConfigs;
                    foreach (var config in watched)
                    {
                        try
                        {
                            CheckForExternalChanges(config);
                        }
                        catch (ThreadAbortException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            Logger.Config.Error($"Error checking {config.File} for external changes");
                            Logger.Config.Error(e);
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // we got aborted :(
            }
        }

        private static void CheckForExternalChanges(Config config)
        {
            bool isExternal;

            lock (config.WriteTimeLock)
            {
                var file = config.File;
                file.Refresh();
                var writeTime = file.Exists ? file.LastWriteTimeUtc : DateTime.MinValue;
                isExternal = writeTime != config.LastKnownWriteTimeUtc;

                if (isExternal)
                {
                    config.LastKnownWriteTimeUtc = writeTime;
                }
            }

            if (isExternal)
            {
                Logger.Config.Notice($"Detected external changes for {config.File.Name}");
                TriggerFileLoad(config);
            }
        }

        public static Task TriggerFileLoad(Config config)
            => loadFactory.StartNew(() => LoadTask(config));

        public static Task TriggerLoadAll()
            => Task.WhenAll(configs.Select(TriggerFileLoad));

        /// <summary>
        /// this is synchronous, unlike <see cref="TriggerFileLoad(Config)"/>
        /// </summary>
        /// <param name="config"></param>
        public static void Save(Config config)
        {
            var store = config.Store;

            try
            {
                using var readLock = Synchronization.LockRead(store.WriteSyncObject);
                lock (config.WriteTimeLock)
                {
                    store.WriteTo(config.configProvider);
                    config.File.Refresh();
                    config.LastKnownWriteTimeUtc = config.File.LastWriteTimeUtc;
                }
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Config.Error($"{nameof(IConfigStore)} for {config.File} errored while writing to disk");
                Logger.Config.Error(e);
            }
        }

        /// <summary>
        /// this is synchronous, unlike <see cref="TriggerLoadAll"/>
        /// </summary>
        public static void SaveAll()
        {
            foreach (var config in configs)
                Save(config);
        }

        private static void LoadTask(Config config)
        { // these tasks will always be running in the same thread as each other
            try
            {
                var store = config.Store;
                using var writeLock = Synchronization.LockWrite(store.WriteSyncObject);
                store.ReadFrom(config.configProvider);
            }
            catch (Exception e)
            {
                Logger.Config.Error($"{nameof(IConfigStore)} for {config.File} errored while reading from the {nameof(IConfigProvider)}");
                Logger.Config.Error(e);
            }
        }

        private static void SaveThread()
        {
            if (requiresSave == null)
            {
                return;
            }

            try
            {
                foreach (var item in requiresSave.GetConsumingEnumerable())
                {
                    try
                    {
                        Save(configs.First((c) => c.Store != null && ReferenceEquals(c.Store.WriteSyncObject, item.WriteSyncObject)));
                    }
                    catch (ThreadAbortException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        Logger.Config.Error($"Error waiting for in-memory updates");
                        Logger.Config.Error(e);
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // we got aborted :(
            }
        }

        private static void LegacySaveThread()
        {
            try
            {
                while (true)
                {
                    var configArr = configs.Where(c => c.Store?.SyncObject != null).ToArray();
                    int index = -1;
                    try
                    {
                        var waitHandles = configArr.Select(c => c.Store.SyncObject)
                                                 .Prepend(configsChangedWatcher)
                                                 .ToArray();
                        index = WaitHandle.WaitAny(waitHandles);
                    }
                    catch (ThreadAbortException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        Logger.Config.Error($"Error waiting for in-memory updates");
                        Logger.Config.Error(e);
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }

                    if (index <= 0)
                    { // we got a signal that the configs collection changed, loop around, or errored
                        continue;
                    }

                    // otherwise, we have a thing that changed in a store
                    Save(configArr[index - 1]);
                }
            }
            catch (ThreadAbortException)
            {
                // we got aborted :(
            }
        }
    }
}

