using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using IllusionPlugin;

namespace IllusionPlugin {
    public class Logger {
        private static BlockingCollection<logMessage> _logQueue;
        private static Thread _watcherThread;
        private static bool _threadRunning;
        private readonly FileInfo _logFile;

        private string ModName;

        struct logMessage {
            public static readonly string logFormat = "[{3} @ {2:HH:mm:ss} | {1}] {0}";

            public WarningLevel WarningLevel;
            public DateTime Time;
            public Logger Log;
            public string Message;

            public logMessage(string msg, Logger log, DateTime time, WarningLevel wl) {
                Message = msg;
                WarningLevel = wl;
                Log = log;
                Time = time;
            }
        }
        
        enum WarningLevel {
            Log, Error, Exception, Warning
        }

        static void SetupStatic()
        {
            if (_logQueue == null)
                _logQueue = new BlockingCollection<logMessage>();
            if (_watcherThread == null || !_watcherThread.IsAlive)
            {
                _watcherThread = new Thread(QueueWatcher); // { IsBackground = true };
                _threadRunning = true;
                _watcherThread.Start();
            }
        }

        public Logger(string modName = "Default") {
            SetupStatic();
            _logFile = GetPath(modName);
            _logFile.Create().Close();
        }

        public Logger(IBeatSaberPlugin plugin)
        {
            SetupStatic();
            _logFile = GetPath(plugin);
            _logFile.Create().Close();
        }

        public void Log(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            //_logQueue.Add(new logMessage($"[LOG @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Log));
            _logQueue.Add(new logMessage(msg, this, DateTime.Now, WarningLevel.Log));
        }
        
        public void Error(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            //_logQueue.Add(new logMessage($"[ERROR @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Error));
            _logQueue.Add(new logMessage(msg, this, DateTime.Now, WarningLevel.Error));
        }
        
        public void Exception(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            //_logQueue.Add(new logMessage($"[EXCEPTION @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Exception));
            _logQueue.Add(new logMessage(msg, this, DateTime.Now, WarningLevel.Exception));
        }
        
        public void Warning(string msg) {
            if(!_watcherThread.IsAlive) throw new Exception("Logger is Closed!");
            //_logQueue.Add(new logMessage($"[WARNING @ {DateTime.Now:HH:mm:ss} | {ModName}] {msg}", WarningLevel.Warning));
            _logQueue.Add(new logMessage(msg, this, DateTime.Now, WarningLevel.Warning));
        }

        static void QueueWatcher() {
            //StreamWriter wstream = null;
            Dictionary<string, StreamWriter> wstreams = new Dictionary<string, StreamWriter>();
            while (_threadRunning && _logQueue.TryTake(out logMessage message, Timeout.Infinite))
            {
                string msg = string.Format(logMessage.logFormat, message.Message, message.Log.ModName, message.Time, message.WarningLevel);

                wstreams[message.Log.ModName] = message.Log._logFile.AppendText();
                wstreams[message.Log.ModName].WriteLine(msg);
                Console.ForegroundColor = GetConsoleColour(message.WarningLevel);
                Console.WriteLine(message.Message);
                Console.ResetColor();

                if (_logQueue.Count == 0)
                { // no more messages
                    foreach (var kvp in wstreams)
                    {
                        if (kvp.Value == null) continue;
                        kvp.Value.Dispose();
                        wstreams[kvp.Key] = null;
                    }
                }
            }

            foreach (var kvp in wstreams)
            {
                if (kvp.Value == null) continue;
                kvp.Value.Dispose();
            }
        }

        public static void Stop() {
            _threadRunning = false;
            _watcherThread.Join();
        }

        static ConsoleColor GetConsoleColour(WarningLevel level) {
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

        FileInfo GetPath(IBeatSaberPlugin plugin) => GetPath(plugin.Name);
        FileInfo GetPath(string modName) {
            ModName = modName;
            var logsDir = new DirectoryInfo($"./Logs/{modName}/{DateTime.Now:dd-MM-yy}");
            logsDir.Create();
            return new FileInfo($"{logsDir.FullName}/{logsDir.GetFiles().Length}.txt");
        }
    }

    public static class LoggerExtensions {
        public static Logger GetLogger(this IBeatSaberPlugin plugin) {
            return new Logger(plugin);
        }
    }
}