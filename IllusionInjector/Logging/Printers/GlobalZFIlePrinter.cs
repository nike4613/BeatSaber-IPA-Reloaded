using IllusionPlugin.Logging;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionInjector.Logging.Printers
{
    public class GlobalZFilePrinter : LogPrinter
    {
        public override IllusionPlugin.Logging.Logger.LogLevel Filter { get; set; } = IllusionPlugin.Logging.Logger.LogLevel.All;

        private FileInfo fileInfo;
        private StreamWriter fileWriter;
        private GZipStream zstream;
        private FileStream fstream;

        private static FileInfo GetFileInfo()
        {
            var logsDir = new DirectoryInfo("Logs");
            logsDir.Create();
            var finfo = new FileInfo(Path.Combine(logsDir.FullName, $"{DateTime.Now:yyyy.MM.dd.HH.MM}.log.z"));
            finfo.Create().Close();
            return finfo;
        }

        public GlobalZFilePrinter()
        {
            fileInfo = GetFileInfo();
        }

        public override void StartPrint()
        {
            fstream = fileInfo.Open(FileMode.Append, FileAccess.Write);
            zstream = new GZipStream(fstream, CompressionMode.Compress)
            {
                FlushMode = FlushType.Full
            };
            fileWriter = new StreamWriter(zstream, Encoding.UTF8);
        }

        public override void Print(IllusionPlugin.Logging.Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (var line in message.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                fileWriter.WriteLine(string.Format("[{3} @ {2:HH:mm:ss} | {1}] {0}", line, logName, time, level.ToString().ToUpper()));
        }

        public override void EndPrint()
        {
            fileWriter.Flush();
            zstream.Flush();
            fstream.Flush();
            fileWriter.Close();
            zstream.Close();
            fstream.Close();
            fileWriter.Dispose();
            zstream.Dispose();
            fstream.Dispose();
        }
    }
}
