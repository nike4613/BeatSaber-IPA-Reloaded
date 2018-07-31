using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionPlugin.Logging.Printers
{
    public class ColoredConsolePrinter : LogPrinter
    {
        Logger.LogLevel filter = Logger.LogLevel.All;
        public override Logger.LogLevel Filter { get => filter; set => filter = value; }

        ConsoleColor color = Console.ForegroundColor;
        public ConsoleColor Color { get => color; set => color = value; }

        public override void Print(Logger.Level level, DateTime time, string logName, string message)
        {
            if (((byte)level & (byte)Filter) == 0) return;
            Console.ForegroundColor = color;
            foreach (var line in message.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine(string.Format(Logger.LogFormat, line, logName, time, level.ToString().ToUpper()));
            Console.ResetColor();
        }
    }
}
