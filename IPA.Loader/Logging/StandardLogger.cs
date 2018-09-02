using IPA.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IPA;
using IPA.Logging.Printers;

namespace IPA.Logging
{
    /// <summary>
    /// The default <see cref="Logger"/> implimentation.
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

        private string logName;
        private static readonly bool showSourceClass = true;
        /// <summary>
        /// All levels defined by this filter will be sent to loggers. All others will be ignored.
        /// </summary>
        public static LogLevel PrintFilter { get; set; } = ModPrefs.GetBool("IPA", "PrintDebug", false, true) ? LogLevel.All : LogLevel.InfoUp;
        private List<LogPrinter> printers = new List<LogPrinter>(defaultPrinters);

        private Dictionary<string, StandardLogger> children = new Dictionary<string, StandardLogger>();

        static StandardLogger()
        {
            showSourceClass = ModPrefs.GetBool("IPA", "DebugShowCallSource", false, true);
        }

        private StandardLogger(string mainName, string subName, params LogPrinter[] inherited)
        {
            logName = $"{mainName}/{subName}";

            printers = new List<LogPrinter>(inherited)
            {
                new PluginSubLogPrinter(mainName, subName)
            };

            if (_logThread == null || !_logThread.IsAlive)
            {
                _logThread = new Thread(LogThread);
                _logThread.Start();
            }
        }

        internal StandardLogger(string name)
        {
            logName = name;

            printers.Add(new PluginLogFilePrinter(name));

            if (_logThread == null || !_logThread.IsAlive)
            {
                _logThread = new Thread(LogThread);
                _logThread.Start();
            }
        }

        internal StandardLogger GetChild(string name)
        {
            if (!children.TryGetValue(name, out StandardLogger chld))
            {
                chld = new StandardLogger(logName, name, printers.ToArray());
                children.Add(name, chld);
            }

            return chld;
        }

        /// <summary>
        /// Logs a specific message at a given level.
        /// </summary>
        /// <param name="level">the message level</param>
        /// <param name="message">the message to log</param>
        public override void Log(Level level, string message)
        {
            _logQueue.Add(new LogMessage
            {
                level = level,
                message = message,
                logger = this,
                time = DateTime.Now
            });
        }
        
        /// <summary>
        /// An override to <see cref="Logger.Debug(string)"/> which shows the method that called it.
        /// </summary>
        /// <param name="message">the message to log</param>
        public override void Debug(string message)
        { // add source to message
            var stfm = new StackTrace().GetFrame(1).GetMethod();
            if (showSourceClass)
                base.Debug($"{{{stfm.DeclaringType.FullName}::{stfm.Name}}} {message}");
            else
                base.Debug(message);
        }

        internal struct LogMessage
        {
            public Level level;
            public StandardLogger logger;
            public string message;
            public DateTime time;
        }

        private static BlockingCollection<LogMessage> _logQueue = new BlockingCollection<LogMessage>();
        private static Thread _logThread;

        private static void LogThread()
        {
            HashSet<LogPrinter> started = new HashSet<LogPrinter>();
            while (_logQueue.TryTake(out LogMessage msg, Timeout.Infinite)) {
                foreach (var printer in msg.logger.printers)
                {
                    try
                    {
                        if (((byte)msg.level & (byte)printer.Filter) != 0)
                        {
                            if (!started.Contains(printer))
                            {
                                printer.StartPrint();
                                started.Add(printer);
                            }

                            printer.Print(msg.level, msg.time, msg.logger.logName, msg.message);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"printer errored {e}");
                    }
                }

                if (_logQueue.Count == 0)
                {
                    foreach (var printer in started)
                    {
                        try
                        {
                            printer.EndPrint();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"printer errored {e}");
                        }
                    }
                    started.Clear();
                }
            }
        }

        internal static void StopLogThread()
        {
            _logQueue.CompleteAdding();
            _logThread.Join();
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
            if (logger is StandardLogger)
            {
                return (logger as StandardLogger).GetChild(name);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
