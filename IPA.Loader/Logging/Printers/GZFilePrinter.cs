using Ionic.Zlib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IPA.Logging.Printers
{
    /// <summary>
    /// A <see cref="LogPrinter"/> abstract class that provides the utilities to write to a GZip file.
    /// </summary>
    public abstract class GZFilePrinter : LogPrinter, IDisposable
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        private FileInfo fileInfo;
        /// <summary>
        /// The <see cref="StreamWriter"/> that writes to the GZip file.
        /// </summary>
        protected StreamWriter FileWriter;
        private GZipStream zstream;
        private FileStream fstream;

        /// <summary>
        /// Gets the <see cref="FileInfo"/> for the file to write to without the .gz extension.
        /// </summary>
        /// <returns></returns>
        protected abstract FileInfo GetFileInfo();

        private void InitLog()
        {
            try
            {
                if (fileInfo == null)
                { // first init
                    fileInfo = GetFileInfo();
                    var ext = fileInfo.Extension;
                    fileInfo = new FileInfo(fileInfo.FullName + ".gz");
                    fileInfo.Create().Close();

                    var symlink = new FileInfo(Path.Combine(fileInfo.DirectoryName ?? throw new InvalidOperationException(), $"latest{ext}.gz"));
                    if (symlink.Exists) symlink.Delete();

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

        /// <summary>
        /// Called at the start of any print session.
        /// </summary>
        public sealed override void StartPrint()
        {
            InitLog();

            fstream = fileInfo.Open(FileMode.Append, FileAccess.Write);
            zstream = new GZipStream(fstream, CompressionMode.Compress)
            {
                FlushMode = FlushType.Full
            };
            FileWriter = new StreamWriter(zstream, new UTF8Encoding(false));
        }

        /// <summary>
        /// Called at the end of any print session.
        /// </summary>
        public sealed override void EndPrint()
        {
            FileWriter.Flush();
            zstream.Flush();
            fstream.Flush();
            FileWriter.Close();
            zstream.Close();
            fstream.Close();
            FileWriter.Dispose();
            zstream.Dispose();
            fstream.Dispose();
        }

        /// <summary>
        /// Disposes the file printer. 
        /// </summary>
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
                zstream.Flush();
                fstream.Flush();
                FileWriter.Close();
                zstream.Close();
                fstream.Close();
                FileWriter.Dispose();
                zstream.Dispose();
                fstream.Dispose();
            }
        }
    }
}
