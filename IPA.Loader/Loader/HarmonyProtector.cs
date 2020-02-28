using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace IPA.Loader
{
    internal static class HarmonyProtectorProxy
    {
        public static void ProtectNull() => HarmonyProtector.Protect();
    }

    internal static class HarmonyProtector
    {
        private static Harmony instance;
        private static Assembly selfAssem;
        private static Assembly harmonyAssem;
        
        public static void Protect(Harmony inst = null)
        {
            selfAssem = Assembly.GetExecutingAssembly();
            harmonyAssem = typeof(Harmony).Assembly;

            if (inst == null)
            {
                if (instance == null)
                    instance = new Harmony("BSIPA Safeguard");

                inst = instance;
            }

            var target = typeof(PatchProcessor).GetMethod("Patch");
            var patch = typeof(HarmonyProtector).GetMethod(nameof(PatchProcessor_Patch_Prefix), BindingFlags.NonPublic | BindingFlags.Static);

            inst.Patch(target, new HarmonyMethod(patch));
        }

        private static bool PatchProcessor_Patch_Prefix(MethodBase ___original, out MethodInfo __result)
        {
            var asm = ___original.DeclaringType.Assembly;

            __result = ___original as MethodInfo;
            return !(asm.Equals(selfAssem) || asm.Equals(harmonyAssem));
        }
    }
}
