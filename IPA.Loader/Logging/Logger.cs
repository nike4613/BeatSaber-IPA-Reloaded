using System;

// ReSharper disable InconsistentNaming

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

        private static StandardLogger _stdout;

        internal static StandardLogger stdout
        {
            get
            {
                if (_stdout == null)
                    _stdout = new StandardLogger("_");
                return _stdout;
            }
        }

        internal static Logger updater => log.GetChildLogger("Updater");
        internal static Logger libLoader => log.GetChildLogger("LibraryLoader");
        internal static Logger injector => log.GetChildLogger("Injector");
        internal static Logger loader => log.GetChildLogger("Loader");
        internal static Logger features => loader.GetChildLogger("Features");
        internal static Logger config => log.GetChildLogger("Config");
        internal static bool LogCreated => _log != null;

        /// <summary>
        /// The standard format for log messages.
        /// </summary>
        /// <value>the format for the standard loggers to print in</value>
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
            /// A trace message. These are ignored *incredibly* early.
            /// </summary>
            Trace = 64,

            /// <summary>
            /// A debug message.
            /// </summary>
            Debug = 1,

            /// <summary>
            /// An informational message.
            /// </summary>
            Info = 2,

            /// <summary>
            /// A notice. More significant than Info, but less than a warning.
            /// </summary>
            Notice = 32,

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
            /// Only shows Trace messages.
            /// </summary>
            TraceOnly = Level.Trace,

            /// <summary>
            /// Only shows Debug messages.
            /// </summary>
            DebugOnly = Level.Debug,

            /// <summary>
            /// Only shows info messages.
            /// </summary>
            InfoOnly = Level.Info,

            /// <summary>
            /// Only shows notice messages.
            /// </summary>
            NoticeOnly = Level.Notice,

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
            /// Shows all messages Notice and up.
            /// </summary>
            NoticeUp = WarningUp | NoticeOnly,

            /// <summary>
            /// Shows all messages info and up.
            /// </summary>
            InfoUp = InfoOnly | NoticeUp,

            /// <summary>
            /// Shows all messages debug and up.
            /// </summary>
            DebugUp = DebugOnly | InfoUp,

            /// <summary>
            /// Shows all messages.
            /// </summary>
            All = TraceOnly | DebugUp,

            /// <summary>
            /// Used for when the level is undefined.
            /// </summary>
            Undefined = byte.MaxValue
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
        /// <param name="e">the exception to log</param>
        public virtual void Log(Level level, Exception e) => Log(level, e.ToString());

        /// <summary>
        /// Sends a trace message.
        /// Equivalent to `Log(Level.Trace, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Trace(string message) => Log(Level.Trace, message);

        /// <summary>
        /// Sends an exception as a trace message.
        /// Equivalent to `Log(Level.Trace, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Trace(Exception e) => Log(Level.Trace, e);

        /// <summary>
        /// Sends a debug message.
        /// Equivalent to `Log(Level.Debug, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Debug(string message) => Log(Level.Debug, message);

        /// <summary>
        /// Sends an exception as a debug message.
        /// Equivalent to `Log(Level.Debug, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Debug(Exception e) => Log(Level.Debug, e);

        /// <summary>
        /// Sends an info message.
        /// Equivalent to `Log(Level.Info, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Info(string message) => Log(Level.Info, message);

        /// <summary>
        /// Sends an exception as an info message.
        /// Equivalent to `Log(Level.Info, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Info(Exception e) => Log(Level.Info, e);

        /// <summary>
        /// Sends a notice message.
        /// Equivalent to `Log(Level.Notice, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Notice(string message) => Log(Level.Notice, message);

        /// <summary>
        /// Sends an exception as a notice message.
        /// Equivalent to `Log(Level.Notice, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Notice(Exception e) => Log(Level.Notice, e);

        /// <summary>
        /// Sends a warning message.
        /// Equivalent to `Log(Level.Warning, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Warn(string message) => Log(Level.Warning, message);

        /// <summary>
        /// Sends an exception as a warning message.
        /// Equivalent to `Log(Level.Warning, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Warn(Exception e) => Log(Level.Warning, e);

        /// <summary>
        /// Sends an error message.
        /// Equivalent to `Log(Level.Error, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Error(string message) => Log(Level.Error, message);

        /// <summary>
        /// Sends an exception as an error message.
        /// Equivalent to `Log(Level.Error, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Error(Exception e) => Log(Level.Error, e);

        /// <summary>
        /// Sends a critical message.
        /// Equivalent to `Log(Level.Critical, message);`
        /// </summary>
        /// <seealso cref="Log(Level, string)"/>
        /// <param name="message">the message to log</param>
        public virtual void Critical(string message) => Log(Level.Critical, message);

        /// <summary>
        /// Sends an exception as a critical message.
        /// Equivalent to `Log(Level.Critical, e);`
        /// </summary>
        /// <seealso cref="Log(Level, Exception)"/>
        /// <param name="e">the exception to log</param>
        public virtual void Critical(Exception e) => Log(Level.Critical, e);
    }
}