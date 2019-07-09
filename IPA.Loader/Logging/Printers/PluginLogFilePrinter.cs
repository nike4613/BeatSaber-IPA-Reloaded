using IPA.Utilities;
using System;
using System.IO;

namespace IPA.Logging.Printers
{
    /// <summary>
    /// Prints log messages to the file specified by the name.
    /// </summary>
    public class PluginLogFilePrinter : GZFilePrinter
    {
        /// <summary>
        /// Provides a filter for this specific printer.
        /// </summary>
        /// <value>the filter level for this printer</value>
        public override Logger.LogLevel Filter { get; set; } = Logger.LogLevel.All;

        private string name;

        /// <summary>
        /// Gets the <see cref="FileInfo"/> for the target file.
        /// </summary>
        /// <returns>the file to write to</returns>
        protected override FileInfo GetFileInfo()
        {
            var logsDir = new DirectoryInfo(Path.Combine("Logs", name));
            logsDir.Create();
            var finfo = new FileInfo(Path.Combine(logsDir.FullName, $"{Utils.CurrentTime():yyyy.MM.dd.HH.mm.ss}.log"));
            return finfo;
        }

        /// <summary>
        /// Creates a new printer with the given name.
        /// </summary>
        /// <param name="name">the name of the logger</param>
        public PluginLogFilePrinter(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Prints an entry to the associated file.
        /// </summary>
        /// <param name="level">the <see cref="Logger.Level"/> of the message</param>
        /// <param name="time">the <see cref="DateTime"/> the message was recorded at</param>
        /// <param name="logName">the name of the log that sent the message</param>
        /// <param name="message">the message to print</param>
        public override void Print(Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (var line in removeControlCodes.Replace(message, "").Split(new[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                FileWriter.WriteLine(Logger.LogFormat, line, logName, time, level.ToString().ToUpper());
        }
    }
}