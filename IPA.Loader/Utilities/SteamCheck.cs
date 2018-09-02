using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Utilities
{
    public static class SteamCheck
    {
        public static Type SteamVRCamera;
        public static Type SteamVRExternalCamera;
        public static Type SteamVRFade;
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
