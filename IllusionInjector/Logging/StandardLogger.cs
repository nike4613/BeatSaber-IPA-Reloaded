using IllusionInjector.Logging.Printers;
using IllusionPlugin.Logging;
using IllusionPlugin.Logging.Printers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LoggerBase = IllusionPlugin.Logging.Logger;

namespace IllusionInjector.Logging
{
    internal static class Logger
    {
        private static LoggerBase _log;
        internal static LoggerBase log
        {
            get
            {
                if (_log == null)
                    _log = new StandardLogger("IllusionInjector");
                return _log;
            }
        }
    }

    public class StandardLogger : LoggerBase
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
            new GlobalZFilePrinter()
        };

        private string logName;
        private static LogLevel showFilter = LogLevel.InfoUp;
        public static LogLevel Filter { get => showFilter; set => showFilter = value; }
        private List<LogPrinter> printers = new List<LogPrinter>(defaultPrinters);

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

        public static void StopLogThread()
        {
            _logQueue.CompleteAdding();
            _logThread.Join();
        }
    }
}
