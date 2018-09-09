using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Utilities
{
    /// <summary>
    /// Provides a utility to test if this is a Steam build of Beat Saber.
    /// </summary>
    [Obsolete("Use BeatSaber.ReleaseType == BeatSaber.Release.Steam")]
    internal static class SteamCheck
    {
        private static Type SteamVRCamera;
        private static Type SteamVRExternalCamera;
        private static Type SteamVRFade;
        /// <summary>
        /// Returns <see langword="true"/> when called on a Steam installation.
        /// </summary>
        public static bool IsAvailable => FindSteamVRAsset();

        private static bool FindSteamVRAsset()
        {
            // these require assembly qualified names....
            SteamVRCamera = Type.GetType("SteamVR_Camera, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            SteamVRExternalCamera = Type.GetType("SteamVR_ExternalCamera, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            SteamVRFade = Type.GetType("SteamVR_Fade, Assembly-CSharp-firstpass, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);

            return SteamVRCamera != null && SteamVRExternalCamera != null && SteamVRFade != null;
        }
    }
}
