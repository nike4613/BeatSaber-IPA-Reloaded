using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using IllusionPlugin;

namespace IllusionPlugin {
    public class Logger {
        private readonly Queue<logMessage> _logQueue;
        private readonly FileInfo _logFile;
        private readonly Thread _watcherThread;
        private bool _threadRunning;

        private string ModName;
        
        private logMessage oldLog;

        struct logMessage {
            public WarningLevel WarningLevel;
            public string Message;

            public logMessage(string msg, WarningLevel wl) {
                Message = msg;
                WarningLevel = wl;
            }
        }
        
        enum WarningLevel {
            Log, Error, Exception, Warning
        }
        
        public Logger(string modName = "Default") {
            _logQueue = new Queue<logMessage>();
            _logFile = GetPath(modName);
            _watcherThread = new Thread(QueueWatcher) {IsBackground = true};
            _threadRunning = true;
            Start();
        }
        
         public Logger(IPlugin plugin) {
            _logQueue = new Queue<logMessage>();
            _logFile = GetPath(plugin);
            _watcherThread = new Thread(QueueWatcher) {IsBackground = true};
            _threadRunning = true;
            Start();
        }

        public void Log(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            _logQueue.Enqueue(new logMessage($"[LOG @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Log));
        }
        
        public void Error(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            _logQueue.Enqueue(new logMessage($"[ERROR @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Error));
        }
        
        public void Exception(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            _logQueue.Enqueue(new logMessage($"[EXCEPTION @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Exception));
        }
        
        public void Warning(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            _logQueue.Enqueue(new logMessage($"[WARNING @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Warning));
        }

        void QueueWatcher() {
            _logFile.Create().Close();
            while (_threadRunning) {
                if (_logQueue.Count > 0) {
                    _watcherThread.IsBackground = false;
                    using (var f = _logFile.AppendText()) {
                        while (_logQueue.Count > 0) {
                            var d = _logQueue.Dequeue();
                            if (d.Message == oldLog.Message) return;
                            oldLog = d;
                            f.WriteLine(d.Message);
                            Console.ForegroundColor = GetConsoleColour(d.WarningLevel);
                            Console.WriteLine(d.Message);
                            Console.ResetColor();
                        }
                    }

                    _watcherThread.IsBackground = true;
                }
            }
            Thread.Sleep(5);
        }

        void Start() => _watcherThread.Start();

        public void Stop() {
            _threadRunning = false;
            _watcherThread.Join();
        }

        ConsoleColor GetConsoleColour(WarningLevel level) {
            switch (level) {
                    case WarningLevel.Log:
                        return ConsoleColor.Green;
                    case WarningLevel.Error:
                        return ConsoleColor.Yellow;
                    case WarningLevel.Exception:
                        return ConsoleColor.Red;
                    case WarningLevel.Warning:
                        return ConsoleColor.Blue;
                    default:
                        return ConsoleColor.Gray;
            }
        }

        FileInfo GetPath(IPlugin plugin) => GetPath(plugin.Name);
        FileInfo GetPath(string modName) {
            ModName = modName;
            var logsDir = new DirectoryInfo($"./Logs/{modName}/{DateTime.Now:dd-MM-yy}");
            logsDir.Create();
            return new FileInfo($"{logsDir.FullName}/{logsDir.GetFiles().Length}.txt");
        }
    }

    public static class DebugExtensions {
        public static Logger GetLogger(this IPlugin plugin) {
            return new Logger(plugin);
        }
    }
}