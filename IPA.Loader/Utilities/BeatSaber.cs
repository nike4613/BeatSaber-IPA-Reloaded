using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Version = SemVer.Version;

namespace IPA.Utilities
{
    /// <summary>
    /// Provides some basic utility methods and properties of Beat Saber
    /// </summary>
    public static class BeatSaber
    {
        private static AlmostVersion _gameVersion;
        /// <summary>
        /// Provides the current game version.
        /// </summary>
        /// <value>the SemVer version of the game</value>
        public static AlmostVersion GameVersion => _gameVersion ?? (_gameVersion = new AlmostVersion(Application.version));

        internal static void SetEarlyGameVersion(AlmostVersion ver)
        {
            _gameVersion = ver;
            Logging.Logger.log.Debug($"GameVersion set early to {ver}");
        }
        internal static void EnsureRuntimeGameVersion()
        {
            var rtVer = new AlmostVersion(Application.version);
            if (!rtVer.Equals(_gameVersion)) // this actually uses stricter equality than == for AlmostVersion
            {
                Logging.Logger.log.Warn($"Early version {_gameVersion} parsed from game files doesn't match runtime version {rtVer}!");
                _gameVersion = rtVer;
            }
        }

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
            /// Indicates an Oculus release.
            /// </summary>
            Oculus
        }
        private static Release? _releaseCache;
        /// <summary>
        /// Gets the <see cref="Release"/> type of this installation of Beat Saber
        /// </summary>
        /// <value>the type of release this is</value>
        public static Release ReleaseType => (_releaseCache ?? (_releaseCache = FindSteamVRAsset() ? Release.Steam : Release.Oculus)).Value;

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

        private static bool FindSteamVRAsset()
        {
            // these require assembly qualified names....
            var steamUser = Type.GetType("Steamworks.SteamUser, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);

            return steamUser != null;
        }
    }
}
