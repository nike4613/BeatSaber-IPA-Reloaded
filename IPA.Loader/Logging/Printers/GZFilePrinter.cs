using Ionic.Zlib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace IPA.Logging.Printers
{
    /// <summary>
    /// A <see cref="LogPrinter"/> abstract class that provides the utilities to write to a GZip file.
    /// </summary>
    public abstract class GZFilePrinter : LogPrinter, IDisposable
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        internal static Regex removeControlCodes = new Regex("\x1b\\[\\d+m", RegexOptions.Compiled);

        private FileInfo fileInfo;

        /// <summary>
        /// The <see cref="StreamWriter"/> that writes to the GZip file.
        /// </summary>
        protected StreamWriter FileWriter;

        private FileStream fstream;

        /// <summary>
        /// Gets the <see cref="FileInfo"/> for the file to write to without the .gz extension.
        /// </summary>
        /// <returns></returns>
        protected abstract FileInfo GetFileInfo();

        private const string latestFormat = "_latest{0}";

        private void InitLog()
        {
            try
            {
                if (fileInfo == null)
                { // first init
                    fileInfo = GetFileInfo();
                    var ext = fileInfo.Extension;

                    var symlink = new FileInfo(Path.Combine(fileInfo.DirectoryName ?? throw new InvalidOperationException(), string.Format(latestFormat, ext)));
                    if (symlink.Exists) symlink.Delete();

                    foreach (var file in fileInfo.Directory.EnumerateFiles("*.log", SearchOption.TopDirectoryOnly))
                    {
                        if (file.Extension == ".gz") continue;

                        CompressOldLog(file);
                    }

                    fileInfo.Create().Close();

                    try
                    {
                        if (!CreateHardLink(symlink.FullName, fileInfo.FullName, IntPtr.Zero))
                        {
                            var error = Marshal.GetLastWin32Error();
                            Logger.log.Error($"Hardlink creation failed ({error})");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.log.Error("Error creating latest hardlink!");
                        Logger.log.Error(e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.log.Error("Error initializing log!");
                Logger.log.Error(e);
            }
        }

        private static async void CompressOldLog(FileInfo file)
        {
            Logger.log.Debug($"Compressing log file {file}");

            var newFile = new FileInfo(file.FullName + ".gz");

            using (var istream = file.OpenRead())
            using (var ostream = newFile.Create())
            using (var gz = new GZipStream(ostream, CompressionMode.Compress, CompressionLevel.BestCompression, false))
                await istream.CopyToAsync(gz);

            file.Delete();
        }

        /// <summary>
        /// Called at the start of any print session.
        /// </summary>
        public sealed override void StartPrint()
        {
            InitLog();

            fstream = fileInfo.Open(FileMode.Append, FileAccess.Write);
            FileWriter = new StreamWriter(fstream, new UTF8Encoding(false));
        }

        /// <summary>
        /// Called at the end of any print session.
        /// </summary>
        public sealed override void EndPrint()
        {
            FileWriter.Flush();
            fstream.Flush();
            FileWriter.Dispose();
            fstream.Dispose();
            FileWriter = null;
            fstream = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the file printer.
        /// </summary>
        /// <param name="disposing">does nothing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                FileWriter.Flush();
                fstream.Flush();
                FileWriter.Close();
                fstream.Close();
                FileWriter.Dispose();
                fstream.Dispose();
            }
        }
    }
}