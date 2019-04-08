using System;
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

        internal static IntPtr OutHandle => outHandle.DangerousGetHandle();
        internal static IntPtr InHandle => inHandle.DangerousGetHandle();

        internal static bool IsInitialized;

        public static void Initialize(bool alwaysCreateNewConsole = true)
        {
            bool consoleAttached = true;
            if (alwaysCreateNewConsole
                || (AttachConsole(AttachParent) == 0
                && Marshal.GetLastWin32Error() != ErrorAccessDenied))
            {
                consoleAttached = AllocConsole() != 0;
            }

            if (consoleAttached)
            {
                InitializeStreams();
                IsInitialized = true;
            }
        }

        public static void InitializeStreams()
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
            var file = new SafeFileHandle(CreateFileW(name, win32DesiredAccess, win32ShareMode, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero), true);
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
        [DllImport("kernel32.dll",
            EntryPoint = "AllocConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll",
            EntryPoint = "AttachConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern uint AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll",
            EntryPoint = "CreateFileW",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr CreateFileW(
              string lpFileName,
              uint dwDesiredAccess,
              uint dwShareMode,
              IntPtr lpSecurityAttributes,
              uint dwCreationDisposition,
              uint dwFlagsAndAttributes,
              IntPtr hTemplateFile
            );

        private const uint GenericWrite = 0x40000000;
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 0x00000003;
        private const uint FileAttributeNormal = 0x80;
        private const uint ErrorAccessDenied = 5;
        
        private const uint AttachParent = 0xFFFFFFFF;

        #endregion
    }
}