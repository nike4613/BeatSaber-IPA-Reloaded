using System;

namespace IPA.Logging.Printers
{
    /// <summary>
    /// Prints a pretty message to the console.
    /// </summary>
    public class ColoredConsolePrinter : LogPrinter
    {
        Logger.LogLevel filter = Logger.LogLevel.All;

        /// <summary>
        /// A filter for this specific printer.
        /// </summary>
        public override Logger.LogLevel Filter { get => filter; set => filter = value; }
        /// <summary>
        /// The color to print messages as.
        /// </summary>
        public ConsoleColor Color { get; set; } = Console.ForegroundColor;

        /// <summary>
        /// Prints an entry to the associated file.
        /// </summary>
        /// <param name="level">the <see cref="Logger.Level"/> of the message</param>
        /// <param name="time">the <see cref="DateTime"/> the message was recorded at</param>
        /// <param name="logName">the name of the log that sent the message</param>
        /// <param name="message">the message to print</param>
        public override void Print(Logger.Level level, DateTime time, string logName, string message)
        {
            if (((byte)level & (byte)StandardLogger.PrintFilter) == 0) return;
            Console.ForegroundColor = Color;
            foreach (var line in message.Split(new[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine(Logger.LogFormat, line, logName, time, level.ToString().ToUpper());
            Console.ResetColor();
        }
    }
}
