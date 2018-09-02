using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Logging
{
    /// <summary>
    /// The logger base class. Provides the format for console logs.
    /// </summary>
    public abstract class Logger
    {
        private static Logger _log;
        internal static Logger log
        {
            get
            {
                if (_log == null)
                    _log = new StandardLogger("IPA");
                return _log;
            }
        }
        internal static bool LogCreated => _log != null;

        /// <summary>
        /// The standard format for log messages.
        /// </summary>
        public static string LogFormat { get; protected internal set; } = "[{3} @ {2:HH:mm:ss} | {1}] {0}";

        /// <summary>
        /// An enum specifying the level of the message. Resembles Syslog.
        /// </summary>
        public enum Level : byte
        {
            /// <summary>
            /// No associated level. These never get shown.
            /// </summary>
            None = 0,
            /// <summary>
            /// A debug message.
            /// </summary>
            Debug = 1,
            /// <summary>
            /// An informational message.
            /// </summary>
            Info = 2,
            /// <summary>
            /// A warning message.
            /// </summary>
            Warning = 4,
            /// <summary>
            /// An error message.
            /// </summary>
            Error = 8,
            /// <summary>
            /// A critical error message.
            /// </summary>
            Critical = 16
        }

        /// <summary>
        /// An enum providing log level filters.
        /// </summary>
        [Flags]
        public enum LogLevel : byte
        {
            /// <summary>
            /// Allow no messages through.
            /// </summary>
            None = Level.None,
            /// <summary>
            /// Only shows Debug messages.
            /// </summary>
            DebugOnly = Level.Debug,
            /// <summary>
            /// Only shows info messages.
            /// </summary>
            InfoOnly = Level.Info,
            /// <summary>
            /// Only shows Warning messages.
            /// </summary>
            WarningOnly = Level.Warning,
            /// <summary>
            /// Only shows Error messages.
            /// </summary>
            ErrorOnly = Level.Error,
            /// <summary>
            /// Only shows Critical messages.
            /// </summary>
            CriticalOnly = Level.Critical,

            /// <summary>
            /// Shows all messages error and up.
            /// </summary>
            ErrorUp = ErrorOnly | CriticalOnly,
            /// <summary>
            /// Shows all messages warning and up.
            /// </summary>
            WarningUp = WarningOnly | ErrorUp,
            /// <summary>
            /// Shows all messages info and up.
            /// </summary>
            InfoUp = InfoOnly | WarningUp,
            /// <summary>
            /// Shows all messages.
            /// </summary>
            All = DebugOnly | InfoUp,
        }

        /// <summary>
        /// A basic log function.
        /// </summary>
        /// <param name="level">the level of the message</param>
        /// <param name="message">the message to log</param>
        public abstract void Log(Level level, string message);
        /// <summary>
        /// A basic log function taking an exception to log.
        /// </summary>
        /// <param name="level">the level of the message</param>
        /// <param name="exeption">the exception to log</param>
        public virtual void Log(Level level, Exception exeption) => Log(level, exeption.ToString());
        /// <summary>
        /// Sends a debug message. 
        /// Equivalent to Log(Level.Debug, message);
        /// <see cref="Log(Level, string)"/>
        /// </summary>
        /// <param name="message">the message to log</param>
        public virtual void Debug(string message) => Log(Level.Debug, message);
        /// <summary>
        /// Sends an exception as a debug message.
        /// Equivalent to Log(Level.Debug, e);
        /// <see cref="Log(Level, Exception)"/>
        /// </summary>
        /// <param name="e">the exception to log</param>
        public virtual void Debug(Exception e) => Log(Level.Debug, e);
        /// <summary>
        /// Sends an info message. 
        /// Equivalent to Log(Level.Info, message).
        /// <see cref="Log(Level, string)"/>
        /// </summary>
        /// <param name="message">the message to log</param>
        public virtual void Info(string message) => Log(Level.Info, message);
        /// <summary>
        /// Sends an exception as an info message.
        /// Equivalent to Log(Level.Info, e);
        /// <see cref="Log(Level, Exception)"/>
        /// </summary>
        /// <param name="e">the exception to log</param>
        public virtual void Info(Exception e) => Log(Level.Info, e);
        /// <summary>
        /// Sends a warning message. 
        /// Equivalent to Log(Level.Warning, message).
        /// <see cref="Log(Level, string)"/>
        /// </summary>
        /// <param name="message">the message to log</param>
        public virtual void Warn(string message) => Log(Level.Warning, message);
        /// <summary>
        /// Sends an exception as a warning message.
        /// Equivalent to Log(Level.Warning, e);
        /// <see cref="Log(Level, Exception)"/>
        /// </summary>
        /// <param name="e">the exception to log</param>
        public virtual void Warn(Exception e) => Log(Level.Warning, e);
        /// <summary>
        /// Sends an error message. 
        /// Equivalent to Log(Level.Error, message).
        /// <see cref="Log(Level, string)"/>
        /// </summary>
        /// <param name="message">the message to log</param>
        public virtual void Error(string message) => Log(Level.Error, message);
        /// <summary>
        /// Sends an exception as an error message.
        /// Equivalent to Log(Level.Error, e);
        /// <see cref="Log(Level, Exception)"/>
        /// </summary>
        /// <param name="e">the exception to log</param>
        public virtual void Error(Exception e) => Log(Level.Error, e);
        /// <summary>
        /// Sends a critical message. 
        /// Equivalent to Log(Level.Critical, message).
        /// <see cref="Log(Level, string)"/>
        /// </summary>
        /// <param name="message">the message to log</param>
        public virtual void Critical(string message) => Log(Level.Critical, message);
        /// <summary>
        /// Sends an exception as a critical message.
        /// Equivalent to Log(Level.Critical, e);
        /// <see cref="Log(Level, Exception)"/>
        /// </summary>
        /// <param name="e">the exception to log</param>
        public virtual void Critical(Exception e) => Log(Level.Critical, e);
    }
}
