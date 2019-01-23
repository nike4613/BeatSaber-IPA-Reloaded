using IPA.Config;
using IPA.Injector.Backups;
using IPA.Loader;
using IPA.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using static IPA.Logging.Logger;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace IPA.Injector
{
    // ReSharper disable once UnusedMember.Global
    public static class Injector
    {
        private static Task pluginAsyncLoadTask;

        // ReSharper disable once UnusedParameter.Global
        public static void Main(string[] args)
        { // entry point for doorstop
          // At this point, literally nothing but mscorlib is loaded,
          // and since this class doesn't have any static fields that
          // aren't defined in mscorlib, we can control exactly what
          // gets loaded.

            try
            {
                if (!Environment.GetCommandLineArgs().Contains("--no-console"))
                    WinConsole.Initialize();

                SetupLibraryLoading();

                EnsureUserData();

                // this is weird, but it prevents Mono from having issues loading the type.
                // IMPORTANT: NO CALLS TO ANY LOGGER CAN HAPPEN BEFORE THIS
                var unused = StandardLogger.PrintFilter;
                #region // Above hack explaination
                /* 
                 * Due to an unknown bug in the version of Mono that Unity 2018.1.8 uses, if the first access to StandardLogger
                 * is a call to a constructor, then Mono fails to load the type correctly. However, if the first access is to
                 * the above static property (or maybe any, but I don't really know) it behaves as expected and works fine.
                 */
                #endregion

                log.Debug("Initializing logger");

                SelfConfig.Set();

                loader.Debug("Prepping bootstrapper");

                InstallBootstrapPatch();

                Updates.InstallPendingUpdates();

                pluginAsyncLoadTask = PluginLoader.LoadTask();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void EnsureUserData()
        {
            string path;
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "UserData")))
                Directory.CreateDirectory(path);
        }

        private static void SetupLibraryLoading()
        {
            if (loadingDone) return;
            loadingDone = true;
            AppDomain.CurrentDomain.AssemblyResolve += LibLoader.AssemblyLibLoader;
        }

        private static void InstallBootstrapPatch()
        {
            var cAsmName = Assembly.GetExecutingAssembly().GetName();

            loader.Debug("Finding backup");
            var backupPath = Path.Combine(Environment.CurrentDirectory, "IPA", "Backups", "Beat Saber");
            var bkp = BackupManager.FindLatestBackup(backupPath);
            if (bkp == null)
                loader.Warn("No backup found! Was BSIPA installed using the installer?");

            loader.Debug("Ensuring patch on UnityEngine.CoreModule exists");

            #region Insert patch into UnityEngine.CoreModule.dll

            {
                var unityPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "Managed",
                    "UnityEngine.CoreModule.dll");

                var unityAsmDef = AssemblyDefinition.ReadAssembly(unityPath, new ReaderParameters
                {
                    ReadWrite = false,
                    InMemory = true,
                    ReadingMode = ReadingMode.Immediate
                });
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

                var cbs = unityModDef.ImportReference(((Action)CreateBootstrapper).Method);

                if (cctor == null)
                {
                    cctor = new MethodDefinition(".cctor",
                        MethodAttributes.RTSpecialName | MethodAttributes.Static | MethodAttributes.SpecialName,
                        unityModDef.TypeSystem.Void);
                    application.Methods.Add(cctor);
                    modified = true;

                    var ilp = cctor.Body.GetILProcessor();
                    ilp.Emit(OpCodes.Call, cbs);
                    ilp.Emit(OpCodes.Ret);
                }
                else
                {
                    var ilp = cctor.Body.GetILProcessor();
                    for (var i = 0; i < Math.Min(2, cctor.Body.Instructions.Count); i++)
                    {
                        var ins = cctor.Body.Instructions[i];
                        switch (i)
                        {
                            case 0 when ins.OpCode != OpCodes.Call:
                                ilp.Replace(ins, ilp.Create(OpCodes.Call, cbs));
                                modified = true;
                                break;

                            case 0:
                                {
                                    var methodRef = ins.Operand as MethodReference;
                                    if (methodRef?.FullName != cbs.FullName)
                                    {
                                        ilp.Replace(ins, ilp.Create(OpCodes.Call, cbs));
                                        modified = true;
                                    }

                                    break;
                                }
                            case 1 when ins.OpCode != OpCodes.Ret:
                                ilp.Replace(ins, ilp.Create(OpCodes.Ret));
                                modified = true;
                                break;
                        }
                    }
                }

                if (modified)
                {
                    bkp?.Add(unityPath);
                    unityAsmDef.Write(unityPath);
                }
            }

            #endregion Insert patch into UnityEngine.CoreModule.dll

            loader.Debug("Ensuring Assembly-CSharp is virtualized");

            #region Virtualize Assembly-CSharp.dll

            {
                var ascPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "Managed",
                    "Assembly-CSharp.dll");

                var ascModule = VirtualizedModule.Load(ascPath);
                ascModule.Virtualize(cAsmName, () => bkp?.Add(ascPath));
            }

            #endregion Virtualize Assembly-CSharp.dll
        }

        private static bool bootstrapped;

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
            WinConsole.InitializeStreams();

            var bootstrapper = new GameObject("NonDestructiveBootstrapper").AddComponent<Bootstrapper>();
            bootstrapper.Destroyed += Bootstrapper_Destroyed;
        }

        private static bool loadingDone;

        private static void Bootstrapper_Destroyed()
        {
            // wait for plugins to finish loading
            pluginAsyncLoadTask.Wait();
            log.Debug("Plugins loaded");
            log.Debug(string.Join(", ", PluginLoader.PluginsMetadata));
            PluginComponent.Create();
        }
    }
}