#nullable enable
using IPA.AntiMalware;
using IPA.Config;
using IPA.Injector.Backups;
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
using static IPA.Logging.Logger;
using MethodAttributes = Mono.Cecil.MethodAttributes;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
#endif

namespace IPA.Injector
{
    /// <summary>
    /// The entry point type for BSIPA's Doorstop injector.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    internal static class Injector
    {
        private static Task? pluginAsyncLoadTask;
        private static Task? permissionFixTask;
        //private static string otherNewtonsoftJson = null;

        // ReSharper disable once UnusedParameter.Global
        internal static void Main(string[] args)
        { // entry point for doorstop
          // At this point, literally nothing but mscorlib is loaded,
          // and since this class doesn't have any static fields that
          // aren't defined in mscorlib, we can control exactly what
          // gets loaded.
            _ = args;
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                MaybeInitializeConsole(arguments);

                SetupLibraryLoading();

                EnsureDirectories();

                // this is weird, but it prevents Mono from having issues loading the type.
                // IMPORTANT: NO CALLS TO ANY LOGGER CAN HAPPEN BEFORE THIS
                var unused = StandardLogger.PrintFilter;
                #region // Above hack explanation
                /*
                 * Due to an unknown bug in the version of Mono that Unity uses, if the first access to StandardLogger
                 * is a call to a constructor, then Mono fails to load the type correctly. However, if the first access is to
                 * the above static property (or maybe any, but I don't really know) it behaves as expected and works fine.
                 */
                #endregion

                Default.Debug("Initializing logger");

                SelfConfig.ReadCommandLine(arguments);
                SelfConfig.Load();
                DisabledConfig.Load();

                if (AntiPiracy.IsInvalid(Environment.CurrentDirectory))
                {
                    Default.Error("Invalid installation; please buy the game to run BSIPA.");

                    return;
                }

                CriticalSection.Configure();

                Logging.Logger.Injector.Debug("Prepping bootstrapper");

                // make sure to load the game version and check boundaries before installing the bootstrap, because that uses the game assemblies property
                GameVersionEarly.Load();
                SelfConfig.Instance.CheckVersionBoundary();

                // updates backup
                InstallBootstrapPatch();

                AntiMalwareEngine.Initialize();

                Updates.InstallPendingUpdates();

                Loader.LibLoader.SetupAssemblyFilenames(true);

                AntiYeet.Initialize();

                pluginAsyncLoadTask = PluginLoader.LoadTask();
                permissionFixTask = PermissionFix.FixPermissions(new DirectoryInfo(Environment.CurrentDirectory));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void MaybeInitializeConsole(string[] arguments)
        {
            var i = 0;
            while (i < arguments.Length)
            {
                if (arguments[i++] == "--verbose")
                {
                    if (i == arguments.Length)
                    {
                        WinConsole.Initialize(WinConsole.AttachParent);
                        return;
                    }

                    WinConsole.Initialize(int.TryParse(arguments[i], out int processId) ? processId : WinConsole.AttachParent);
                    return;
                }
            }
        }

        private static void EnsureDirectories()
        {
            string path;
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "UserData")))
                _ = Directory.CreateDirectory(path);
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "Plugins")))
                _ = Directory.CreateDirectory(path);
        }

        private static void SetupLibraryLoading()
        {
            if (loadingDone) return;
            loadingDone = true;
            Loader.LibLoader.Configure();
        }

        private static void InstallBootstrapPatch()
        {
            var sw = Stopwatch.StartNew();

            var cAsmName = Assembly.GetExecutingAssembly().GetName();
            var managedPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            var dataDir = new DirectoryInfo(managedPath).Parent!.Name;
            var gameName = dataDir.Substring(0, dataDir.Length - 5);

            Logging.Logger.Injector.Debug("Finding backup");
            var backupPath = Path.Combine(Environment.CurrentDirectory, "IPA", "Backups", gameName);
            var bkp = BackupManager.FindLatestBackup(backupPath);
            if (bkp == null)
                Logging.Logger.Injector.Warn("No backup found! Was BSIPA installed using the installer?");

            // TODO: Investigate if this ever worked properly.
            // this is a critical section because if you exit in here, assembly can die
            using var critSec = CriticalSection.ExecuteSection();

            var readerParameters = new ReaderParameters
            {
                ReadWrite = false,
                InMemory = true,
                ReadingMode = ReadingMode.Immediate
            };

            Logging.Logger.Injector.Debug("Ensuring patch on UnityEngine.CoreModule exists");

            #region Insert patch into UnityEngine.CoreModule.dll

            var unityPath = Path.Combine(managedPath, "UnityEngine.CoreModule.dll");

            using var unityAsmDef = AssemblyDefinition.ReadAssembly(unityPath, readerParameters);
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

            var application = unityModDef.GetType("UnityEngine", "Camera");

            if (application == null)
            {
                Logging.Logger.Injector.Critical("UnityEngine.CoreModule doesn't have a definition for UnityEngine.Camera!"
                    + "Nothing to patch to get ourselves into the Unity run cycle!");
                goto endPatchCoreModule;
            }

            MethodDefinition? cctor = null;
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
                string tempFilePath = Path.GetTempFileName();
                bkp?.Add(unityPath);
                unityAsmDef.Write(tempFilePath);
                File.Delete(unityPath);
                File.Move(tempFilePath, unityPath);
            }
            endPatchCoreModule:
            #endregion Insert patch into UnityEngine.CoreModule.dll

            sw.Stop();
            Logging.Logger.Injector.Info($"Installing bootstrapper took {sw.Elapsed}");
        }

        private static bool bootstrapped;

        private static void CreateBootstrapper()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            Application.logMessageReceivedThreaded += delegate (string condition, string stackTrace, LogType type)
            {
                var level = UnityLogRedirector.LogTypeToLevel(type);
                UnityLogProvider.UnityLogger.Log(level, $"{condition}");
                UnityLogProvider.UnityLogger.Log(level, $"{stackTrace}");
            };

            StdoutInterceptor.EnsureHarmonyLogging();

            // need to reinit streams singe Unity seems to redirect stdout
            StdoutInterceptor.RedirectConsole();

            var bootstrapper = new GameObject("NonDestructiveBootstrapper").AddComponent<Bootstrapper>();
            bootstrapper.Destroyed += Bootstrapper_Destroyed;
        }

        private static bool loadingDone;

        private static void Bootstrapper_Destroyed()
        {
            // wait for plugins to finish loading
            pluginAsyncLoadTask?.Wait();
            permissionFixTask?.Wait();

            Default.Debug("Plugins loaded");
            Default.Debug(string.Join(", ", PluginLoader.PluginsMetadata.StrJP()));
            _ = PluginComponent.Create();
        }
    }
}
