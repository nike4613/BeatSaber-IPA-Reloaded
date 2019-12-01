using Harmony;
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
        private static HarmonyInstance instance;
        private static Assembly selfAssem;
        private static Assembly harmonyAssem;
        
        public static void Protect(HarmonyInstance inst = null)
        {
            selfAssem = Assembly.GetExecutingAssembly();
            harmonyAssem = typeof(HarmonyInstance).Assembly;

            if (inst == null)
            {
                if (instance == null)
                    instance = HarmonyInstance.Create("BSIPA Safeguard");

                inst = instance;
            }

            var target = typeof(PatchProcessor).GetMethod("Patch");
            var patch = typeof(HarmonyProtector).GetMethod(nameof(PatchProcessor_Patch_Prefix));

            inst.Patch(target, new HarmonyMethod(patch));
        }

        private static void PatchProcessor_Patch_Prefix(ref List<MethodBase> ___originals)
        {
            for (int i = 0; i < ___originals.Count; i++)
            {
                var mi = ___originals[i];
                var asm = mi.DeclaringType.Assembly;

                if (asm.Equals(selfAssem) || asm.Equals(harmonyAssem))
                    ___originals.RemoveAt(i--);
            }
        }
    }
}
