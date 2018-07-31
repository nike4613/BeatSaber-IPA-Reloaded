using IllusionPlugin.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionInjector.Logging.Printers
{
    public class PluginLogFilePrinter : LogPrinter
    {
        public override IllusionPlugin.Logging.Logger.LogLevel Filter { get; set; }

        private FileInfo fileInfo;
        private StreamWriter fileWriter;

        private static FileInfo GetFileInfo(string modName)
        {
            var logsDir = new DirectoryInfo(Path.Combine("Logs",modName));
            logsDir.Create();
            var finfo = new FileInfo(Path.Combine(logsDir.FullName, $"{DateTime.Now:YYYY.MM.DD.HH.MM}.log"));
            finfo.CreateText().Close();
            return finfo;
        }

        public PluginLogFilePrinter(string name)
        {
            fileInfo = GetFileInfo(name);
        }

        public override void StartPrint()
        {
            fileWriter = fileInfo.AppendText();
        }

        public override void Print(IllusionPlugin.Logging.Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (var line in message.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                fileWriter.WriteLine(string.Format("[{3} @ {2:HH:mm:ss}] {0}", line, logName, time, level.ToString().ToUpper()));
        }

        public override void EndPrint()
        {
            fileWriter.Dispose();
        }
    }
}
