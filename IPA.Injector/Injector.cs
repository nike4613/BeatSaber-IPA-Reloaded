using Harmony;
using IPA.Injector.Backups;
using IPA.Loader;
using IPA.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static IPA.Logging.Logger;
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
                if (!Environment.GetCommandLineArgs().Contains("--no-console"))
                    Windows.WinConsole.Initialize();

                SetupLibraryLoading();

                loader.Debug("Prepping bootstrapper");
                
                InstallBootstrapPatch();

                Updates.InstallPendingUpdates();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void InstallBootstrapPatch()
        {
            var cAsmName = Assembly.GetExecutingAssembly().GetName();

            loader.Debug("Finding backup");
            var backupPath = Path.Combine(Environment.CurrentDirectory, "IPA","Backups","Beat Saber");
            var bkp = BackupManager.FindLatestBackup(backupPath);
            if (bkp == null)
                loader.Warn("No backup found! Was BSIPA installed using the installer?");

            loader.Debug("Ensuring patch on UnityEngine.CoreModule exists");
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
                    if (i == 0)
                    {
                        if (ins.OpCode != OpCodes.Call)
                        {
                            ilp.Replace(ins, ilp.Create(OpCodes.Call, cbs));
                            modified = true;
                        }
                        else
                        {
                            var mref = ins.Operand as MethodReference;
                            if (mref.FullName != cbs.FullName)
                            {
                                ilp.Replace(ins, ilp.Create(OpCodes.Call, cbs));
                                modified = true;
                            }
                        }
                    }
                    if (i == 1 && ins.OpCode != OpCodes.Ret)
                    {
                        ilp.Replace(ins, ilp.Create(OpCodes.Ret));
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                bkp?.Add(unityPath);
                unityAsmDef.Write(unityPath);
            }
            #endregion

            loader.Debug("Ensuring Assembly-CSharp is virtualized");
            #region Virtualize Assembly-CSharp.dll
            var ascPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "Managed", "Assembly-CSharp.dll");
            
            var ascModule = VirtualizedModule.Load(ascPath);
            ascModule.Virtualize(cAsmName, () => bkp?.Add(ascPath));
            #endregion
        }

        private static bool bootstrapped = false;
        private static void CreateBootstrapper()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            Application.logMessageReceived += delegate (string condition, string stackTrace, LogType type)
            {
                var level = UnityLogInterceptor.LogTypeToLevel(type);
                UnityLogInterceptor.UnityLogger.Log(level, $"{condition.Trim()}");
                UnityLogInterceptor.UnityLogger.Log(level, $"{stackTrace.Trim()}");
            };

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
            /*try
            {
                if (!SetDllDirectory(LibLoader.NativeDir))
                {
                    libLoader.Warn("Unable to add native library path to load path");
                }
            }
            catch (Exception) { }*/
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
