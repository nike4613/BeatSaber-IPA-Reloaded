using IPA.Logging;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Logging.Printers
{
    public abstract class GZFilePrinter : LogPrinter
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        [DllImport("Kernel32.dll")]
        static extern Int32 GetLastError();

        private FileInfo fileInfo;
        protected StreamWriter fileWriter;
        private GZipStream zstream;
        private FileStream fstream;

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

                    var symlink = new FileInfo(Path.Combine(fileInfo.DirectoryName, $"latest{ext}.gz"));
                    if (symlink.Exists) symlink.Delete();

                    try
                    {
                        if (!CreateHardLink(symlink.FullName, fileInfo.FullName, IntPtr.Zero))
                        {
                            Logger.log.Error($"Hardlink creation failed {GetLastError()}");
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

        public override sealed void StartPrint()
        {
            InitLog();

            fstream = fileInfo.Open(FileMode.Append, FileAccess.Write);
            zstream = new GZipStream(fstream, CompressionMode.Compress)
            {
                FlushMode = FlushType.Full
            };
            fileWriter = new StreamWriter(zstream, new UTF8Encoding(false));
        }

        public override sealed void EndPrint()
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
