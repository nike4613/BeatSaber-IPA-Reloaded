using IPA.Logging;
using System;
using System.IO;

namespace IPA.Utilities
{
    internal static class DoorstopEnvironment
    {
        public static readonly string InvokeDLLPath;
        public static readonly string ProcessPath;
        public static readonly string ManagedFolderDir;
        public static readonly string MonoLibPath;
        public static readonly string[] DLLSearchDirs;

        static DoorstopEnvironment()
        {
            InvokeDLLPath = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH")!;
            ProcessPath = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH")!;
            ManagedFolderDir = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR")!;
            MonoLibPath = Environment.GetEnvironmentVariable("DOORSTOP_MONO_LIB_PATH")!;
            DLLSearchDirs = Environment.GetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS")?.Split(Path.PathSeparator) ?? Array.Empty<string>();

            Logger.Log(Logger.LoggerId.Default, Logger.Level.Debug, $"{nameof(InvokeDLLPath)}: {InvokeDLLPath}");
            Logger.Log(Logger.LoggerId.Default, Logger.Level.Debug, $"{nameof(ProcessPath)}: {ProcessPath}");
            Logger.Log(Logger.LoggerId.Default, Logger.Level.Debug, $"{nameof(ManagedFolderDir)}: {ManagedFolderDir}");
            Logger.Log(Logger.LoggerId.Default, Logger.Level.Debug, $"{nameof(MonoLibPath)}: {MonoLibPath}");
            Logger.Log(Logger.LoggerId.Default, Logger.Level.Debug, $"{nameof(DLLSearchDirs)}: {string.Join(Path.PathSeparator.ToString(), DLLSearchDirs)}");
        }
    }
}
