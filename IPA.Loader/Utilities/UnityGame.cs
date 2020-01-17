using IPA.Config;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;
#if NET3
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Utilities
{
    /// <summary>
    /// Provides some basic utility methods and properties of Beat Saber
    /// </summary>
    public static class UnityGame
    {
        private static AlmostVersion _gameVersion;
        /// <summary>
        /// Provides the current game version.
        /// </summary>
        /// <value>the SemVer version of the game</value>
        public static AlmostVersion GameVersion => _gameVersion ?? (_gameVersion = new AlmostVersion(ApplicationVersionProxy));

        internal static void SetEarlyGameVersion(AlmostVersion ver)
        {
            _gameVersion = ver;
            Logging.Logger.log.Debug($"GameVersion set early to {ver}");
        }
        private static string ApplicationVersionProxy => Application.version;
        internal static void EnsureRuntimeGameVersion()
        {
            try
            {
                var rtVer = new AlmostVersion(ApplicationVersionProxy);
                if (!rtVer.Equals(_gameVersion)) // this actually uses stricter equality than == for AlmostVersion
                {
                    Logging.Logger.log.Warn($"Early version {_gameVersion} parsed from game files doesn't match runtime version {rtVer}!");
                    _gameVersion = rtVer;
                }
            }
            catch (MissingMethodException e)
            {
                Logging.Logger.log.Error("Application.version was not found! Cannot check early parsed version");
                if (SelfConfig.Debug_.ShowHandledErrorStackTraces_)
                    Logging.Logger.log.Error(e);
            }
        }

        internal static bool IsGameVersionBoundary { get; private set; }
        internal static AlmostVersion OldVersion { get; private set; }
        internal static void CheckGameVersionBoundary()
        {
            var gameVer = GameVersion;
            var lastVerS = SelfConfig.LastGameVersion_;
            OldVersion = lastVerS != null ? new AlmostVersion(lastVerS, gameVer) : null;

            IsGameVersionBoundary = OldVersion != null && gameVer != OldVersion;

            SelfConfig.Instance.LastGameVersion = gameVer.ToString();
        }

        private static Thread mainThread;
        /// <summary>
        /// Checks if the currently running code is running on the Unity main thread.
        /// </summary>
        /// <value><see langword="true"/> if the curent thread is the Unity main thread, <see langword="false"/> otherwise</value>
        public static bool OnMainThread => Thread.CurrentThread.ManagedThreadId == mainThread?.ManagedThreadId;

        internal static void SetMainThread() 
            => mainThread = Thread.CurrentThread;

        /// <summary>
        /// The different types of releases of the game.
        /// </summary>
        public enum Release
        {
            /// <summary>
            /// Indicates a Steam release.
            /// </summary>
            Steam,
            /// <summary>
            /// Indicates a non-Steam release.
            /// </summary>
            Other
        }
        private static Release? _releaseCache;
        /// <summary>
        /// Gets the <see cref="Release"/> type of this installation of Beat Saber
        /// </summary>
        /// <remarks>
        /// This only gives a
        /// </remarks>
        /// <value>the type of release this is</value>
        public static Release ReleaseType => (_releaseCache ?? (_releaseCache = CheckIsSteam() ? Release.Steam : Release.Other)).Value;

        private static string _installRoot;
        /// <summary>
        /// Gets the path to the Beat Saber install directory.
        /// </summary>
        /// <value>the path of the game install directory</value>
        public static string InstallPath
        {
            get
            {
                if (_installRoot == null)
                    _installRoot = Path.GetFullPath(
                        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", ".."));
                return _installRoot;
            }
        }
        /// <summary>
        /// The path to the `Libs` folder. Use only if necessary.
        /// </summary>
        /// <value>the path to the library directory</value>
        public static string LibraryPath => Path.Combine(InstallPath, "Libs");
        /// <summary>
        /// The path to the `Libs\Native` folder. Use only if necessary.
        /// </summary>
        /// <value>the path to the native library directory</value>
        public static string NativeLibraryPath => Path.Combine(LibraryPath, "Native");
        /// <summary>
        /// The directory to load plugins from.
        /// </summary>
        /// <value>the path to the plugin directory</value>
        public static string PluginsPath => Path.Combine(InstallPath, "Plugins");
        /// <summary>
        /// The path to the `UserData` folder.
        /// </summary>
        /// <value>the path to the user data directory</value>
        public static string UserDataPath => Path.Combine(InstallPath, "UserData");

        private static bool CheckIsSteam()
        {
            var installDirInfo = new DirectoryInfo(InstallPath);
            return installDirInfo.Parent?.Name == "common"
                && installDirInfo.Parent?.Parent?.Name == "steamapps";
        }
    }
}
