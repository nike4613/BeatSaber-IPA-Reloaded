using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IllusionPlugin.Logging;
using LoggerBase = IllusionPlugin.Logging.Logger;

namespace IllusionInjector.Logging.Printers
{
    class GlobalLogFilePrinter : GZFilePrinter
    {
        public override LoggerBase.LogLevel Filter { get; set; } = LoggerBase.LogLevel.All;

        public override void Print(IllusionPlugin.Logging.Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (var line in message.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                fileWriter.WriteLine(string.Format(LoggerBase.LogFormat, line, logName, time, level.ToString().ToUpper()));
        }

        protected override FileInfo GetFileInfo()
        {
            var logsDir = new DirectoryInfo("Logs");
            logsDir.Create();
            var finfo = new FileInfo(Path.Combine(logsDir.FullName, $"{DateTime.Now:yyyy.MM.dd.HH.mm}.log"));
            return finfo;
        }
    }
}
