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
        private static SingleThreadTaskScheduler writeScheduler = null;
        private static TaskFactory writeFactory = null;
        private static Thread readThread = null;

        private static void TryStartRuntime()
        {
            if (writeScheduler == null || !writeScheduler.IsRunning)
            {
                writeFactory = null;
                writeScheduler = new SingleThreadTaskScheduler();
                writeScheduler.Start();
            }
            if (writeFactory == null)
                writeFactory = new TaskFactory(writeScheduler);
            if (readThread == null || !readThread.IsAlive)
            {
                readThread = new Thread(ReadThread);
                readThread.Start();
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
                writeFactory.StartNew(() => WriteTask(config).Wait());
        }

        private static async Task WriteTask(Config config)
        {

        } 

        private static void ReadThread()
        {

        }
    }
}
