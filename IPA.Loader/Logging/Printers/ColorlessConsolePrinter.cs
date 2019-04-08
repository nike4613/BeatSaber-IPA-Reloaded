using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Logging.Printers
{
    /// <summary>
    /// A colorless version of <see cref="ColoredConsolePrinter"/>, that indiscriminantly prints to standard out.
    /// </summary>
    public class ColorlessConsolePrinter : LogPrinter
    {
        /// <summary>
        /// A filter for this specific printer.
        /// </summary>
        public override Logger.LogLevel Filter { get; set; }

        /// <summary>
        /// Prints an entry to standard out.
        /// </summary>
        /// <param name="level">the <see cref="Logger.Level"/> of the message</param>
        /// <param name="time">the <see cref="DateTime"/> the message was recorded at</param>
        /// <param name="logName">the name of the log that sent the message</param>
        /// <param name="message">the message to print</param>
        public override void Print(Logger.Level level, DateTime time, string logName, string message)
        {
            if (((byte)level & (byte)StandardLogger.PrintFilter) == 0) return;
            foreach (var line in message.Split(new[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine(Logger.LogFormat, line, logName, time, level.ToString().ToUpper());
        }
    }
}
