using IPA.Config;
using IPA.Logging.Printers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace IPA.Logging
{
    /// <summary>
    /// The default (and standard) <see cref="Logger"/> implementation.
    /// </summary>
    /// <remarks>
    /// <see cref="StandardLogger"/> uses a multi-threaded approach to logging. All actual I/O is done on another thread,
    /// where all messaged are guaranteed to be logged in the order they appeared. It is up to the printers to format them.
    ///
    /// This logger supports child loggers. Use <see cref="LoggerExtensions.GetChildLogger"/> to safely get a child.
    /// The modification of printers on a parent are reflected down the chain.
    /// </remarks>
    public class StandardLogger : Logger
    {
        private static readonly List<LogPrinter> defaultPrinters = new List<LogPrinter>()
        {
            new ColoredConsolePrinter()
            {
                Filter = LogLevel.DebugOnly,
                Color = ConsoleColor.Green,
            },
            new ColoredConsolePrinter()
            {
                Filter = LogLevel.InfoOnly,
                Color = ConsoleColor.White,
            },
            new ColoredConsolePrinter()
            {
                Filter = LogLevel.WarningOnly,
                Color = ConsoleColor.Yellow,
            },
            new ColoredConsolePrinter()
            {
                Filter = LogLevel.ErrorOnly,
                Color = ConsoleColor.Red,
            },
            new ColoredConsolePrinter()
            {
                Filter = LogLevel.CriticalOnly,
                Color = ConsoleColor.Magenta,
            },
            new GlobalLogFilePrinter()
        };

        /// <summary>
        /// Adds to the default printer pool that all printers inherit from. Printers added this way will be passed every message from every logger.
        /// </summary>
        /// <param name="printer"></param>
        internal static void AddDefaultPrinter(LogPrinter printer)
        {
            defaultPrinters.Add(printer);
        }

        private readonly string logName;
        private static bool showSourceClass;

        /// <summary>
        /// All levels defined by this filter will be sent to loggers. All others will be ignored.
        /// </summary>
        public static LogLevel PrintFilter { get; set; } = LogLevel.All;

        private readonly List<LogPrinter> printers = new List<LogPrinter>();
        private readonly StandardLogger parent;

        private readonly Dictionary<string, StandardLogger> children = new Dictionary<string, StandardLogger>();

        /// <summary>
        /// Configures internal debug settings based on the config passed in.
        /// </summary>
        /// <param name="cfg"></param>
        internal static void Configure(SelfConfig cfg)
        {
            showSourceClass = cfg.Debug.ShowCallSource;
            PrintFilter = cfg.Debug.ShowDebug ? LogLevel.All : LogLevel.InfoUp;
        }

        private StandardLogger(StandardLogger parent, string subName)
        {
            logName = $"{parent.logName}/{subName}";
            this.parent = parent;
            printers = new List<LogPrinter>()
            {
                new PluginSubLogPrinter(parent.logName, subName)
            };

            if (logThread == null || !logThread.IsAlive)
            {
                logThread = new Thread(LogThread);
                logThread.Start();
            }
        }

        internal StandardLogger(string name)
        {
            logName = name;
            printers.Add(new PluginLogFilePrinter(name));

            if (logThread == null || !logThread.IsAlive)
            {
                logThread = new Thread(LogThread);
                logThread.Start();
            }
        }

        /// <summary>
        /// Gets a child printer with the given name, either constructing a new one or using one that was already made.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>a child <see cref="StandardLogger"/> with the given sub-name</returns>
        internal StandardLogger GetChild(string name)
        {
            if (!children.TryGetValue(name, out var child))
            {
                child = new StandardLogger(this, name);
                children.Add(name, child);
            }

            return child;
        }

        /// <summary>
        /// Adds a log printer to the logger.
        /// </summary>
        /// <param name="printer">the printer to add</param>
        public void AddPrinter(LogPrinter printer)
        {
            printers.Add(printer);
        }

        /// <summary>
        /// Logs a specific message at a given level.
        /// </summary>
        /// <param name="level">the message level</param>
        /// <param name="message">the message to log</param>
        public override void Log(Level level, string message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            logQueue.Add(new LogMessage
            {
                Level = level,
                Message = message,
                Logger = this,
                Time = DateTime.Now
            });
        }

        /// <inheritdoc />
        /// <summary>
        /// An override to <see cref="M:IPA.Logging.Logger.Debug(System.String)" /> which shows the method that called it.
        /// </summary>
        /// <param name="message">the message to log</param>
        public override void Debug(string message)
        {
            // add source to message
            var stackFrame = new StackTrace(true).GetFrame(1);
            var method = stackFrame.GetMethod();
            var lineNo = stackFrame.GetFileLineNumber();
            var paramString = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName));
            base.Debug(showSourceClass
                ? $"{{{method.DeclaringType?.FullName}::{method.Name}({paramString}):{lineNo}}} {message}"
                : message);
        }

        private struct LogMessage
        {
            public Level Level;
            public StandardLogger Logger;
            public string Message;
            public DateTime Time;
        }

        private static readonly BlockingCollection<LogMessage> logQueue = new BlockingCollection<LogMessage>();
        private static Thread logThread;

        /// <summary>
        /// The log printer thread for <see cref="StandardLogger"/>.
        /// </summary>
        private static void LogThread()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                StopLogThread();
            };

            var started = new HashSet<LogPrinter>();
            while (logQueue.TryTake(out var msg, Timeout.Infinite))
            {
                do
                {
                    var logger = msg.Logger;
                    IEnumerable<LogPrinter> printers = logger.printers;
                    do
                    {
                        logger = logger.parent;
                        if (logger != null)
                            printers = printers.Concat(logger.printers);
                    } while (logger != null);

                    foreach (var printer in printers.Concat(defaultPrinters))
                    {
                        try
                        {
                            if (((byte) msg.Level & (byte) printer.Filter) != 0)
                            {
                                if (!started.Contains(printer))
                                {
                                    printer.StartPrint();
                                    started.Add(printer);
                                }

                                printer.Print(msg.Level, msg.Time, msg.Logger.logName, msg.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"printer errored: {e}");
                        }
                    }
                }
                // wait for messages for 500ms before ending the prints
                while (logQueue.TryTake(out msg, TimeSpan.FromMilliseconds(500)));

                if (logQueue.Count == 0)
                {
                    foreach (var printer in started)
                    {
                        try
                        {
                            printer.EndPrint();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"printer errored: {e}");
                        }
                    }
                    started.Clear();
                }
            }
        }

        /// <summary>
        /// Stops and joins the log printer thread.
        /// </summary>
        internal static void StopLogThread()
        {
            logQueue.CompleteAdding();
            logThread.Join();
        }
    }

    /// <summary>
    /// A class providing extensions for various loggers.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Gets a child logger, if supported. Currently the only defined and supported logger is <see cref="StandardLogger"/>, and most plugins will only ever receive this anyway.
        /// </summary>
        /// <param name="logger">the parent <see cref="Logger"/></param>
        /// <param name="name">the name of the child</param>
        /// <returns>the child logger</returns>
        public static Logger GetChildLogger(this Logger logger, string name)
        {
            if (logger is StandardLogger standardLogger)
                return standardLogger.GetChild(name);

            throw new InvalidOperationException();
        }
    }
}