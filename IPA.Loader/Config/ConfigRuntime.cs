using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using IPA.Utilities;
using IPA.Utilities.Async;
using System.IO;
using System.Runtime.CompilerServices;
using IPA.Logging;

namespace IPA.Config
{
    internal static class ConfigRuntime
    {
        private class DirInfoEqComparer : IEqualityComparer<DirectoryInfo>
        {
            public bool Equals(DirectoryInfo x, DirectoryInfo y)
                => x?.FullName == y?.FullName;

            public int GetHashCode(DirectoryInfo obj)
                => obj?.GetHashCode() ?? 0;
        }

        private static readonly ConcurrentBag<Config> configs = new ConcurrentBag<Config>();
        private static readonly AutoResetEvent configsChangedWatcher = new AutoResetEvent(false);
        private static readonly ConcurrentDictionary<DirectoryInfo, FileSystemWatcher> watchers 
            = new ConcurrentDictionary<DirectoryInfo, FileSystemWatcher>(new DirInfoEqComparer());
        private static readonly ConditionalWeakTable<FileSystemWatcher, ConcurrentBag<Config>> watcherTrackConfigs
            = new ConditionalWeakTable<FileSystemWatcher, ConcurrentBag<Config>>();
        private static SingleThreadTaskScheduler loadScheduler = null;
        private static TaskFactory loadFactory = null;
        private static Thread saveThread = null;

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
        }

        public static void RegisterConfig(Config cfg)
        {
            lock (configs)
            { // we only lock this segment, so that this only waits on other calls to this
                if (configs.ToArray().Contains(cfg))
                    throw new InvalidOperationException("Config already registered to runtime!");

                configs.Add(cfg);
            }
            configsChangedWatcher.Set();

            TryStartRuntime();

            AddConfigToWatchers(cfg);
        }

        private static void AddConfigToWatchers(Config config)
        {
            var dir = config.File.Directory;
            if (!watchers.TryGetValue(dir, out var watcher))
            { // create the watcher
                watcher = new FileSystemWatcher(dir.FullName, "");
                var newWatcher = watchers.GetOrAdd(dir, watcher);
                if (watcher != newWatcher)
                { // if someone else beat us to adding, delete ours and switch to that new one
                    watcher.Dispose();
                    watcher = newWatcher;
                }

                watcher.NotifyFilter =
                    NotifyFilters.FileName
                    | NotifyFilters.LastWrite;

                watcher.Changed += FileChangedEvent;
                watcher.Created += FileChangedEvent;
                watcher.Renamed += FileChangedEvent;
                watcher.Deleted += FileChangedEvent;
            }

            TryStartRuntime();

            watcher.EnableRaisingEvents = false; // disable while we do shit

            var bag = watcherTrackConfigs.GetOrCreateValue(watcher);
            // we don't need to check containment because this function will only be called once per config ever
            bag.Add(config);

            watcher.EnableRaisingEvents = true;

        }

        private static void FileChangedEvent(object sender, FileSystemEventArgs e)
        {
            var watcher = sender as FileSystemWatcher;
            if (!watcherTrackConfigs.TryGetValue(watcher, out var bag)) return;

            var config = bag.FirstOrDefault(c => c.File.FullName == e.FullPath);
            if (config != null)
                TriggerFileLoad(config);
        }

        public static Task TriggerFileLoad(Config config) => loadFactory.StartNew(() => LoadTask(config));

        private static void LoadTask(Config config)
        { // these tasks will always be running in the same thread as each other
            try
            {
                var store = config.Store;
                using var writeLock = Synchronization.LockWrite(store.WriteSyncObject);
                lock (config.Provider)
                {
                    config.Provider.File = config.File;
                    store.ReadFrom(config.Provider);
                }
            }
            catch (Exception e)
            {
                Logger.config.Error($"{nameof(IConfigStore)} for {config.File} errored while reading from the {nameof(IConfigProvider)}");
                Logger.config.Error(e);
            }
        } 

        private static void SaveThread()
        {
            while (true)
            {
                var configArr = configs.ToArray();
                var waitHandles = configArr.Select(c => c.Store.SyncObject)
                                         .Prepend(configsChangedWatcher)
                                         .ToArray();
                var index = WaitHandle.WaitAny(waitHandles);

                if (index == 0)
                { // we got a signal that the configs collection changed, loop around
                    continue;
                }

                // otherwise, we have a thing that changed in a store
                var config = configArr[index - 1];
                var store = config.Store;

                try
                {
                    using var readLock = Synchronization.LockRead(store.WriteSyncObject);
                    lock (config.Provider)
                    {
                        config.Provider.File = config.File;
                        store.WriteTo(config.Provider);
                    }
                }
                catch (Exception e)
                {
                    Logger.config.Error($"{nameof(IConfigStore)} for {config.File} errored while writing to disk");
                    Logger.config.Error(e);
                }
            }
        }
    }
}
