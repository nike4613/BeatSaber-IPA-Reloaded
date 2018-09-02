using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IPA.Logging;

namespace IPA.Logging.Printers
{
    class GlobalLogFilePrinter : GZFilePrinter
    {
        public override Logger.LogLevel Filter { get; set; } = Logger.LogLevel.All;

        public override void Print(IPA.Logging.Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (var line in message.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                fileWriter.WriteLine(string.Format(Logger.LogFormat, line, logName, time, level.ToString().ToUpper()));
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
