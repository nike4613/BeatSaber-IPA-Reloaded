using IllusionPlugin.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zlib;

namespace IllusionInjector.Logging.Printers
{
    public class GlobalZFIlePrinter : LogPrinter
    {
        public override IllusionPlugin.Logging.Logger.LogLevel Filter { get; set; }

        private FileInfo fileInfo;
        private StreamWriter fileWriter;

        private static FileInfo GetFileInfo()
        {
            var logsDir = new DirectoryInfo("Logs");
            logsDir.Create();
            var finfo = new FileInfo(Path.Combine(logsDir.FullName, $"{DateTime.Now:YYYY.MM.DD.HH.MM}.log.z"));
            finfo.Create().Close();
            return finfo;
        }

        public GlobalZFIlePrinter()
        {
            fileInfo = GetFileInfo();
        }

        public override void StartPrint()
        {
            fileWriter = new StreamWriter(
                new ZOutputStream(fileInfo.Open(FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                     FlushMode = zlibConst.Z_FULL_FLUSH
                },
                Encoding.UTF8
            );
        }

        public override void Print(IllusionPlugin.Logging.Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (var line in message.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                fileWriter.WriteLine(string.Format("[{3} @ {2:HH:mm:ss} | {1}] {0}", line, logName, time, level.ToString().ToUpper()));
        }

        public override void EndPrint()
        {
            fileWriter.Dispose();
        }
    }
}
