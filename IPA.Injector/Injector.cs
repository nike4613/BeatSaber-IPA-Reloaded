using Harmony;
using IPA.Loader;
using IPA.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static IPA.Logging.Logger;
using Logger = IPA.Logging.Logger;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace IPA.Injector
{
    public static class Injector
    {
        public static void Main(string[] args)
        { // entry point for doorstop
          // At this point, literally nothing but mscorlib is loaded,
          // and since this class doesn't have any static fields that 
          // aren't defined in mscorlib, we can control exactly what 
          // gets loaded.

            try
            {
                // This loads System.Runtime.InteropServices, and Microsoft.Win32.SafeHandles.
                Windows.WinConsole.Initialize();

                // This loads AppDomain, System.IO, System.Collections.Generic, and System.Reflection.
                // If kernel32.dll is not already loaded, this will also load it.
                // This call also loads IPA.Loader and initializes the logging system. In the process
                // it loads Ionic.Zip.
                SetupLibraryLoading();

                loader.Debug("Prepping bootstrapper");

                // // This will load Mono.Cecil
                InstallBootstrapPatch();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void InstallBootstrapPatch()
        {
            var cAsmName = Assembly.GetExecutingAssembly().GetName();

            #region Insert patch into UnityEngine.CoreModule.dll
            var unityPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "Managed", "UnityEngine.CoreModule.dll");

            var unityAsmDef = AssemblyDefinition.ReadAssembly(unityPath);
            var unityModDef = unityAsmDef.MainModule;

            bool modified = false;
            foreach (var asmref in unityModDef.AssemblyReferences)
            {
                if (asmref.Name == cAsmName.Name)
                {
                    if (asmref.Version != cAsmName.Version)
                    {
                        asmref.Version = cAsmName.Version;
                        modified = true;
                    }
                }
            }

            var application = unityModDef.GetType("UnityEngine", "Application");

            MethodDefinition cctor = null;
            foreach (var m in application.Methods)
                if (m.IsRuntimeSpecialName && m.Name == ".cctor")
                    cctor = m;

            var cbs = unityModDef.Import(((Action)CreateBootstrapper).Method);

            if (cctor == null)
            {
                cctor = new MethodDefinition(".cctor", MethodAttributes.RTSpecialName | MethodAttributes.Static | MethodAttributes.SpecialName, unityModDef.TypeSystem.Void);
                application.Methods.Add(cctor);
                modified = true;

                var ilp = cctor.Body.GetILProcessor();
                ilp.Emit(OpCodes.Call, cbs);
                ilp.Emit(OpCodes.Ret);
            }
            else
            {
                var ilp = cctor.Body.GetILProcessor();
                for (int i = 0; i < Math.Min(2, cctor.Body.Instructions.Count); i++)
                {
                    var ins = cctor.Body.Instructions[i];
                    if (i == 0 && (ins.OpCode != OpCodes.Call || ins.Operand != cbs))
                    {
                        ilp.Replace(ins, ilp.Create(OpCodes.Call, cbs));
                        modified = true;
                    }
                    if (i == 1 && ins.OpCode != OpCodes.Ret)
                    {
                        ilp.Replace(ins, ilp.Create(OpCodes.Ret));
                        modified = true;
                    }
                }
            }

            if (modified)
                unityAsmDef.Write(unityPath);
            #endregion
        }

        private static bool bootstrapped = false;
        private static void CreateBootstrapper()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            // need to reinit streams singe Unity seems to redirect stdout
            Windows.WinConsole.InitializeStreams();

            var bootstrapper = new GameObject("NonDestructiveBootstrapper").AddComponent<Bootstrapper>();
            bootstrapper.Destroyed += Bootstrapper_Destroyed;
        }

        private static bool injected = false;
        public static void Inject()
        {
            if (!injected)
            {
                injected = true;
                Windows.WinConsole.Initialize();
                SetupLibraryLoading();
                var bootstrapper = new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
                bootstrapper.Destroyed += Bootstrapper_Destroyed;
            }
        }

        private static bool loadingDone = false;
        public static void SetupLibraryLoading()
        {
            if (loadingDone) return;
            loadingDone = true;
            #region Add Library load locations
            AppDomain.CurrentDomain.AssemblyResolve += LibLoader.AssemblyLibLoader;
            try
            {
                if (!SetDllDirectory(LibLoader.NativeDir))
                {
                    libLoader.Warn("Unable to add native library path to load path");
                }
            }
            catch (Exception) { }
            #endregion
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetDllDirectory(string lpPathName);

        private static void Bootstrapper_Destroyed()
        {
            PluginComponent.Create();
        }
    }
}
