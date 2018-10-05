using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SemVer;
using Version = SemVer.Version;

namespace IPA.Utilities
{
    /// <summary>
    /// Provides some basic utility methods and properties of Beat Saber
    /// </summary>
    public static class BeatSaber
    {
        private static Version _gameVersion = null;
        /// <summary>
        /// Provides the current game version
        /// </summary>
        public static Version GameVersion => _gameVersion ?? (_gameVersion = new Version(UnityEngine.Application.version));

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
        private static Release? _releaseCache = null;
        /// <summary>
        /// Gets the <see cref="Release"/> type of this installation of Beat Saber
        /// </summary>
        public static Release ReleaseType => (_releaseCache ?? (_releaseCache = FindSteamVRAsset() ? Release.Steam : Release.Oculus)).Value;

        /// <summary>
        /// The path to the `Libs` folder. Use only if necessary.
        /// </summary>
        public static string LibraryPath => Path.Combine(Environment.CurrentDirectory, "Libs");
        /// <summary>
        /// The path to the `Libs\Native` folder. Use only if necessary.
        /// </summary>
        public static string NativeLibraryPath => Path.Combine(LibraryPath, "Native");

        private static bool FindSteamVRAsset()
        {
            // these require assembly qualified names....
            var SteamVRCamera = Type.GetType("SteamVR_Camera, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            var SteamVRExternalCamera = Type.GetType("SteamVR_ExternalCamera, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            var SteamVRFade = Type.GetType("SteamVR_Fade, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);

            return SteamVRCamera != null && SteamVRExternalCamera != null && SteamVRFade != null;
        }
    }
}
