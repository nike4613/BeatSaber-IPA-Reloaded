using HarmonyLib;
using System;
using System.Reflection;

namespace IPA.Injector
{
    internal static class AntiYeet
    {
        public static void Initialize()
        {
#if BeatSaber
            AppDomain.CurrentDomain.AssemblyLoad += ApplyPatchOnAssemblyLoad;
#endif
        }

        private static void ApplyPatchOnAssemblyLoad(object sender, AssemblyLoadEventArgs e)
        {
            var targetMethod = e.LoadedAssembly.GetType("IPAPluginsDirDeleter")?.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (targetMethod != null)
            {
                Patch.Apply(targetMethod);
                AppDomain.CurrentDomain.AssemblyLoad -= ApplyPatchOnAssemblyLoad;
            }
        }

        private static class Patch
        {
            private static Harmony harmony;

            public static void Apply(MethodInfo original)
            {
                Logging.Logger.Injector.Info("Applying anti-yeet patch");

                try
                {
                    harmony = new Harmony("BSIPA Anti-Yeet");
                    harmony.Patch(original, new HarmonyMethod(typeof(Patch).GetMethod(nameof(SuppressIPAPluginsDirDeleter), BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception ex)
                {
                    Logging.Logger.Injector.Error("Could not apply anti-yeet patch");
                    Logging.Logger.Injector.Error(ex);
                }
            }

            private static bool SuppressIPAPluginsDirDeleter()
            {
                return false;
            }
        }
    }
}