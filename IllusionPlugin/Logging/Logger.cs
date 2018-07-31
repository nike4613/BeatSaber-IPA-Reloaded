using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionPlugin.Logging
{
    public abstract class Logger
    {
        public static string LogFormat { get; protected internal set; } = "[{3} @ {2:HH:mm:ss} | {1}] {0}";

        public enum Level : byte
        {
            None = 0,
            Debug = 1,
            Info = 2,
            Warning = 4,
            Error = 8,
            Critical = 16
        }

        [Flags]
        public enum LogLevel : byte
        {
            None = Level.None,
            DebugOnly = Level.Debug,
            InfoOnly = Level.Info,
            WarningOnly = Level.Warning,
            ErrorOnly = Level.Error,
            CriticalOnly = Level.Critical,

            ErrorUp = ErrorOnly | CriticalOnly,
            WarningUp = WarningOnly | ErrorUp,
            InfoUp = InfoOnly | WarningUp,
            All = DebugOnly | InfoUp,
        }

        public abstract void Log(Level level, string message);
        public void Log(Level level, Exception exeption) => Log(level, exeption.ToString());
        public void Debug(string message) => Log(Level.Debug, message);
        public void Debug(Exception e) => Log(Level.Debug, e);
        public void Info(string message) => Log(Level.Info, message);
        public void Info(Exception e) => Log(Level.Info, e);
        public void Warn(string message) => Log(Level.Warning, message);
        public void Warn(Exception e) => Log(Level.Warning, e);
        public void Error(string message) => Log(Level.Error, message);
        public void Error(Exception e) => Log(Level.Error, e);
        public void Critical(string message) => Log(Level.Critical, message);
        public void Critical(Exception e) => Log(Level.Critical, e);
    }
}
