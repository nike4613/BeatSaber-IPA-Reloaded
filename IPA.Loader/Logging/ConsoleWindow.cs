using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace IPA.Logging
{
    // https://stackoverflow.com/a/48864902/3117125
    internal static class WinConsole
    {
        internal static TextWriter ConOut;
        internal static TextReader ConIn;

        private static SafeFileHandle outHandle;
        private static SafeFileHandle inHandle;

        public static bool UseVTEscapes { get; private set; } = true;

        internal static IntPtr OutHandle => outHandle.DangerousGetHandle();
        internal static IntPtr InHandle => inHandle.DangerousGetHandle();

        internal static bool IsInitialized;

        public static void Initialize(int processId, bool alwaysCreateNewConsole = false)
        {
            bool consoleAttached;
            if (alwaysCreateNewConsole || !(consoleAttached = AttachConsole(processId)))
            {
                consoleAttached = AllocConsole();
            }

            if (consoleAttached)
            {
                InitializeStreams();
                IsInitialized = true;
            }
        }

        private static void InitializeStreams()
        {
            InitializeOutStream();
            InitializeInStream();
        }

        private static void InitializeOutStream()
        {
            var fs = CreateFileStream("CONOUT$", GenericWrite, FileShareWrite, FileAccess.Write, out outHandle);
            if (fs != null)
            {
                var writer = new StreamWriter(fs) { AutoFlush = true };
                ConOut = writer;
                Console.SetOut(writer);
                Console.SetError(writer);

                var handle = GetStdHandle(-11); // get stdout handle (should be CONOUT$ at this point)
                if (GetConsoleMode(handle, out var mode))
                {
                    mode |= EnableVTProcessing;
                    if (!SetConsoleMode(handle, mode))
                    {
                        UseVTEscapes = false;
                        Console.Error.WriteLine("Could not enable VT100 escape code processing (maybe you're running an old Windows?): " +
                            new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    }
                }
                else
                {
                    UseVTEscapes = false;
                    Console.Error.WriteLine("Could not enable VT100 escape code processing (maybe you're running an old Windows?): " +
                        new Win32Exception(Marshal.GetLastWin32Error()).Message);
                }
            }
        }

        private static void InitializeInStream()
        {
            var fs = CreateFileStream("CONIN$", GenericRead, FileShareRead, FileAccess.Read, out inHandle);
            if (fs != null)
            {
                Console.SetIn(ConIn = new StreamReader(fs));
            }
        }

        private static FileStream CreateFileStream(string name, uint win32DesiredAccess, uint win32ShareMode,
                                FileAccess dotNetFileAccess, out SafeFileHandle handle)
        {
            var file = new SafeFileHandle(CreateFile(name, win32DesiredAccess, win32ShareMode, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero), true);
            if (!file.IsInvalid)
            {
                handle = file;
                var fs = new FileStream(file, dotNetFileAccess);
                return fs;
            }

            handle = null;
            return null;
        }

        #region Win API Functions and Constants

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        private const uint EnableVTProcessing = 0x0004;

        private const uint GenericWrite = 0x40000000;
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 0x00000003;
        private const uint FileAttributeNormal = 0x80;

        internal const int AttachParent = -1;

        #endregion
    }
}