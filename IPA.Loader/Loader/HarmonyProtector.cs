using HarmonyLib;
using IPA.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace IPA.Loader
{
    internal static class HarmonyProtectorProxy
    {
        public static void ProtectNull() => HarmonyProtector.Protect();
    }

    internal static class HarmonyProtector
    {
        public static void Protect()
        {
            var guid = Guid.NewGuid().ToString("N");
            var id = guid.Remove(new Random().Next(7, guid.Length - 1));
            var harmony = new Harmony(id);

            var unpatchByTypeOrId = AccessTools.Method(typeof(PatchProcessor), nameof(PatchProcessor.Unpatch), new[] { typeof(HarmonyPatchType), typeof(string) });
            var unpatchMethod = AccessTools.Method(typeof(PatchProcessor), nameof(PatchProcessor.Unpatch), new[] { typeof(MethodInfo) });
            var processPatchJob = AccessTools.Method(typeof(PatchClassProcessor), "ProcessPatchJob");
            var patch = AccessTools.Method(typeof(PatchProcessor), nameof(PatchProcessor.Patch));

            var unpatchPrefix = AccessTools.Method(typeof(HarmonyProtector), nameof(PatchProcessor_Unpatch_Prefix));
            var processPatchJobPrefix = AccessTools.Method(typeof(HarmonyProtector), nameof(PatchClassProcessor_ProcessPatchJob_Prefix));
            var patchPrefix = AccessTools.Method(typeof(HarmonyProtector), nameof(PatchProcessor_Patch_Prefix));

            harmony.Patch(unpatchByTypeOrId, new HarmonyMethod(unpatchPrefix));
            harmony.Patch(unpatchMethod, new HarmonyMethod(unpatchPrefix));
            harmony.Patch(processPatchJob, new HarmonyMethod(processPatchJobPrefix));
            harmony.Patch(patch, new HarmonyMethod(patchPrefix));
        }

        private static bool ShouldBlockExecution(MethodBase methodBase)
        {
            var getIdentifiable = AccessTools.Method(typeof(DetourHelper), nameof(DetourHelper.GetIdentifiable)).GetIdentifiable();
            var getValue = AccessTools.Method(typeof(FieldInfo), nameof(FieldInfo.GetValue)).GetIdentifiable();
            var declaringTypeGetter = AccessTools.PropertyGetter(typeof(MemberInfo), nameof(MemberInfo.DeclaringType)).GetIdentifiable();
            var methodBaseEquals = AccessTools.Method(typeof(MethodBase), nameof(MethodBase.Equals)).GetIdentifiable();
            var assemblyEquals = AccessTools.Method(typeof(Assembly), nameof(Assembly.Equals)).GetIdentifiable();
            var assemblyGetter = AccessTools.PropertyGetter(typeof(Type), nameof(Type.Assembly)).GetIdentifiable();
            var getExecutingAssembly = AccessTools.Method(typeof(Assembly), nameof(Assembly.GetExecutingAssembly)).GetIdentifiable();
            var method = methodBase.GetIdentifiable();
            var assembly = method.DeclaringType!.Assembly;
            return method.Equals(getIdentifiable) ||
                   method.Equals(getValue) ||
                   method.Equals(declaringTypeGetter) ||
                   method.Equals(methodBaseEquals) ||
                   method.Equals(assemblyEquals) ||
                   method.Equals(assemblyGetter) ||
                   method.Equals(getExecutingAssembly) ||
                   assembly.Equals(Assembly.GetExecutingAssembly()) ||
                   assembly.Equals(typeof(Harmony).Assembly);
        }

        private static bool PatchProcessor_Patch_Prefix(MethodBase ___original, ref MethodInfo __result)
        {
            if (ShouldBlockExecution(___original))
            {
                __result = ___original as MethodInfo;
                return false;
            }

            return true;
        }

        private static bool PatchClassProcessor_ProcessPatchJob_Prefix(object job)
        {
            var original = AccessTools.Field(job.GetType(), "original");
            var methodBase = (MethodBase)original!.GetValue(job);
            return !ShouldBlockExecution(methodBase);
        }

        private static bool PatchProcessor_Unpatch_Prefix(MethodBase ___original) => !ShouldBlockExecution(___original);
    }
}
