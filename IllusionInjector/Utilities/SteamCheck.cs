using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionInjector.Utilities
{
    public static class SteamCheck
    {
        public static Type SteamVRCamera;
        public static Type SteamVRExternalCamera;
        public static Type SteamVRFade;
        public static bool IsAvailable => FindSteamVRAsset();

        private static bool FindSteamVRAsset()
        {
            SteamVRCamera = Type.GetType("SteamVR_Camera", false);
            SteamVRExternalCamera = Type.GetType("SteamVR_ExternalCamera", false);
            SteamVRFade = Type.GetType("SteamVR_Fade", false);
            return SteamVRCamera != null && SteamVRExternalCamera != null && SteamVRFade != null;
        }
    }
}
