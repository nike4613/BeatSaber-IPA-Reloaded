using HarmonyLib;
using System;

namespace IPA.Injector
{
    internal static class AntiYeetPatch
    {
        private static Harmony instance;

        public static void Apply()
        {
#if BeatSaber
            Logging.Logger.Injector.Info("Applying anti-yeet patch");

            try
            {
                instance = new Harmony("BSIPA Anti-Yeet");

                var original = AccessTools.Method("IPAPluginsDirDeleter:Awake");
                var prefix = new HarmonyMethod(AccessTools.Method(typeof(AntiYeetPatch), nameof(SuppressIPAPluginsDirDeleter)));
                instance.Patch(original, prefix);
            }
            catch (Exception e)
            {
                Logging.Logger.Injector.Warn("Could not apply anti-yeet patch");
                Logging.Logger.Injector.Warn(e);
            }
#endif
        }

        private static bool SuppressIPAPluginsDirDeleter() => false;
    }
}