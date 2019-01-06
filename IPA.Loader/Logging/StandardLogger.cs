using IPA.Config;
using IPA.Logging.Printers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace IPA.Logging
{
    /// <summary>
    /// The default <see cref="Logger"/> implementation.
    /// </summary>
    public class StandardLogger : Logger
    {
        private static readonly IReadOnlyList<LogPrinter> defaultPrinters = new List<LogPrinter>()
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

        private readonly string logName;
        private static bool showSourceClass;

        /// <summary>
        /// All levels defined by this filter will be sent to loggers. All others will be ignored.
        /// </summary>
        public static LogLevel PrintFilter { get; set; } = LogLevel.All;
        private readonly List<LogPrinter> printers = new List<LogPrinter>(defaultPrinters);

        private readonly Dictionary<string, StandardLogger> children = new Dictionary<string, StandardLogger>();
        
        internal static void Configure(SelfConfig cfg)
        {
            showSourceClass = cfg.Debug.ShowCallSource;
            PrintFilter = cfg.Debug.ShowDebug ? LogLevel.All : LogLevel.InfoUp;
        }

        private StandardLogger(string mainName, string subName, params LogPrinter[] inherited)
        {
            logName = $"{mainName}/{subName}";
            printers = new List<LogPrinter>(inherited)
            {
                new PluginSubLogPrinter(mainName, subName)
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

        internal StandardLogger GetChild(string name)
        {
            if (!children.TryGetValue(name, out var child))
            {
                child = new StandardLogger(logName, name, printers.ToArray());
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
            var stackFrame = new StackTrace().GetFrame(1);
            var method = stackFrame.GetMethod();
            var lineNo = stackFrame.GetFileLineNumber();
            var lineOffs = stackFrame.GetFileColumnNumber();
            base.Debug(showSourceClass
                ? $"{{{method.DeclaringType?.FullName}::{method.Name}({lineNo}:{lineOffs})}} {message}"
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

        private static void LogThread()
        {
            var started = new HashSet<LogPrinter>();
            while (logQueue.TryTake(out var msg, Timeout.Infinite)) {
                foreach (var printer in msg.Logger.printers)
                {
                    try
                    {
                        if (((byte)msg.Level & (byte)printer.Filter) != 0)
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
        /// Gets a child logger, if supported.
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
