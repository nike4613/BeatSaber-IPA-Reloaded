#nullable enable
using IPA.Loader;
using IPA.Logging;
using IPA.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Logger = IPA.Logging.Logger;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace IPA.Injector
{
    internal static class Bootstrap
    {
        private static Task? pluginAsyncLoadTask;

        public static void InstallPatch()
        {
            var sw = Stopwatch.StartNew();

            var readerParameters = new ReaderParameters
            {
                ReadWrite = false,
                InMemory = true,
                ReadingMode = ReadingMode.Immediate
            };

            var unityPath = Path.Combine(DoorstopEnvironment.ManagedFolderDir, "UnityEngine.CoreModule.dll");

            using var unityAsmDef = AssemblyDefinition.ReadAssembly(unityPath, readerParameters);
            var unityModDef = unityAsmDef.MainModule;
            var display = unityModDef.GetType(nameof(UnityEngine), nameof(Display));

            if (display == null)
            {
                Logger.Injector.Critical("Cannot find UnityEngine.Display; plugins won't be loaded.");
                goto endPatchCoreModule;
            }

            PatchStaticConstructor(unityModDef, display, CreateBootstrapper);

            var unityLogWriterInit = unityModDef.GetType(nameof(UnityEngine), "ClassLibraryInitializer")
                ?.Methods.FirstOrDefault(m => m.Name == "Init");
            if (unityLogWriterInit != null)
            {
                PatchMethod(unityModDef, unityLogWriterInit, RedirectLogging);
            }
            else
            {
                Logger.Injector.Error("Cannot find UnityEngine.ClassLibraryInitializer.Init; Unity logs won't be redirected.");
            }

            Logger.Injector.Info($"Loading {unityModDef.Name} from memory...");
            LibLoader.LoadFromMemory(unityAsmDef, unityPath);
            endPatchCoreModule:

            sw.Stop();
            Logger.Injector.Info($"Installing bootstrapper took {sw.Elapsed}");
        }

        public static void LoadPlugins()
        {
            pluginAsyncLoadTask = PluginLoader.LoadTask();
        }

        private static void PatchStaticConstructor(ModuleDefinition moduleDefinition, TypeDefinition typeDefinition, Action action)
        {
            var cctor = typeDefinition.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
            var methodReference = moduleDefinition.ImportReference(action.Method);
            ILProcessor il;

            if (cctor == null)
            {
                cctor = new MethodDefinition(ConstructorInfo.TypeConstructorName, MethodAttributes.Private |
                    MethodAttributes.Static | MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, moduleDefinition.TypeSystem.Void);
                typeDefinition.Attributes &= ~TypeAttributes.BeforeFieldInit;
                typeDefinition.Methods.Add(cctor);

                il = cctor.Body.GetILProcessor();
                il.Emit(OpCodes.Call, methodReference);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                il = cctor.Body.GetILProcessor();
                il.InsertBefore(cctor.Body.Instructions[0], il.Create(OpCodes.Call, methodReference));
            }
        }

        private static void PatchMethod(ModuleDefinition moduleDefinition, MethodDefinition method, Action action)
        {
            var methodReference = moduleDefinition.ImportReference(action.Method);
            var il = method.Body.GetILProcessor();

            var rets = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
            foreach (var ret in rets)
            {
                ret.OpCode = OpCodes.Call;
                ret.Operand = methodReference;
                il.InsertAfter(ret, il.Create(OpCodes.Ret));
            }
        }

        private static void RedirectLogging()
        {
            UnityLogProvider.RedirectUnityLogging();
            StdoutInterceptor.RedirectHarmonyLogging();
        }

        private static void CreateBootstrapper()
        {
            var bootstrapper = new GameObject("NonDestructiveBootstrapper").AddComponent<Bootstrapper>();
            bootstrapper.Destroyed += InitializePlugins;
        }

        private static void InitializePlugins()
        {
            // wait for plugins to finish loading
            pluginAsyncLoadTask?.Wait();

            Logger.Default.Debug("Plugins loaded");
            Logger.Default.Debug(PluginLoader.PluginsMetadata.ToString());
            _ = PluginComponent.Create();
        }
    }
}
