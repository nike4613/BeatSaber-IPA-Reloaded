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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using static IPA.Logging.Logger;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace IPA.Injector
{
    /// <summary>
    /// The entry point type for BSIPA's Doorstop injector.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    internal static class Injector
    {
        private static Task pluginAsyncLoadTask;
        private static Task permissionFixTask;

        // ReSharper disable once UnusedParameter.Global
        internal static void Main(string[] args)
        { // entry point for doorstop
          // At this point, literally nothing but mscorlib is loaded,
          // and since this class doesn't have any static fields that
          // aren't defined in mscorlib, we can control exactly what
          // gets loaded.

            try
            {
                if (Environment.GetCommandLineArgs().Contains("--verbose"))
                    WinConsole.Initialize();

                SetupLibraryLoading();

                EnsureDirectories();

                // this is weird, but it prevents Mono from having issues loading the type.
                // IMPORTANT: NO CALLS TO ANY LOGGER CAN HAPPEN BEFORE THIS
                var unused = StandardLogger.PrintFilter;
                #region // Above hack explaination
                /* 
                 * Due to an unknown bug in the version of Mono that Unity uses, if the first access to StandardLogger
                 * is a call to a constructor, then Mono fails to load the type correctly. However, if the first access is to
                 * the above static property (or maybe any, but I don't really know) it behaves as expected and works fine.
                 */
                #endregion

                log.Debug("Initializing logger");

                SelfConfig.Load();
                DisabledConfig.Load();

                loader.Debug("Prepping bootstrapper");
                
                // updates backup
                InstallBootstrapPatch();

                Updates.InstallPendingUpdates();

                LibLoader.SetupAssemblyFilenames(true);

                // causes mono to hate itself
                //GameVersionEarly.Load();

                pluginAsyncLoadTask = PluginLoader.LoadTask();
                permissionFixTask = PermissionFix.FixPermissions(new DirectoryInfo(Environment.CurrentDirectory));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void EnsureDirectories()
        {
            string path;
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "UserData")))
                Directory.CreateDirectory(path);
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "Plugins")))
                Directory.CreateDirectory(path);
        }

        private static void SetupLibraryLoading()
        {
            if (loadingDone) return;
            loadingDone = true;
            AppDomain.CurrentDomain.AssemblyResolve += LibLoader.AssemblyLibLoader;
            LibLoader.SetupAssemblyFilenames(true);
        }

        private static void InstallBootstrapPatch()
        {
            var cAsmName = Assembly.GetExecutingAssembly().GetName();
            var managedPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var dataDir = new DirectoryInfo(managedPath).Parent.Name;
            var gameName = dataDir.Substring(0, dataDir.Length - 5);

            loader.Debug("Finding backup");
            var backupPath = Path.Combine(Environment.CurrentDirectory, "IPA", "Backups", gameName);
            var bkp = BackupManager.FindLatestBackup(backupPath);
            if (bkp == null)
                loader.Warn("No backup found! Was BSIPA installed using the installer?");

            loader.Debug("Ensuring patch on UnityEngine.CoreModule exists");

            #region Insert patch into UnityEngine.CoreModule.dll

            {
                var unityPath = Path.Combine(managedPath,
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
                /*else
                    return; // shortcut*/
            }

            #endregion Insert patch into UnityEngine.CoreModule.dll

            loader.Debug("Ensuring Assembly-CSharp is virtualized");
            
            {
                var ascPath = Path.Combine(managedPath,
                    "Assembly-CSharp.dll");

                #region Virtualize Assembly-CSharp.dll

                {
                    var ascModule = VirtualizedModule.Load(ascPath);
                    ascModule.Virtualize(cAsmName, () => bkp?.Add(ascPath));
                }

                #endregion Virtualize Assembly-CSharp.dll

                #region Anti-Yeet

                //if (SelfConfig.SelfConfigRef.Value.ApplyAntiYeet)
                try
                {
                    loader.Debug("Applying anti-yeet patch");
                    
                    var ascAsmDef = AssemblyDefinition.ReadAssembly(ascPath, new ReaderParameters
                    {
                        ReadWrite = false,
                        InMemory = true,
                        ReadingMode = ReadingMode.Immediate
                    });
                    var ascModDef = ascAsmDef.MainModule;

                    var deleter = ascModDef.GetType("IPAPluginsDirDeleter");
                    deleter.Methods.Clear(); // delete all methods

                    ascAsmDef.Write(ascPath);
                }
                catch (Exception)
                {
                    // ignore
                }

                #endregion
            }
        }

        private static bool bootstrapped;

        private static void CreateBootstrapper()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            Application.logMessageReceived += delegate (string condition, string stackTrace, LogType type)
            {
                var level = UnityLogRedirector.LogTypeToLevel(type);
                UnityLogProvider.UnityLogger.Log(level, $"{condition}");
                UnityLogProvider.UnityLogger.Log(level, $"{stackTrace}");
            };

            // need to reinit streams singe Unity seems to redirect stdout
            StdoutInterceptor.RedirectConsole();

            var bootstrapper = new GameObject("NonDestructiveBootstrapper").AddComponent<Bootstrapper>();
            bootstrapper.Destroyed += Bootstrapper_Destroyed;
        }

        private static bool loadingDone;

        private static void Bootstrapper_Destroyed()
        {
            // wait for plugins to finish loading
            pluginAsyncLoadTask.Wait();
            permissionFixTask.Wait();
            log.Debug("Plugins loaded");
            log.Debug(string.Join(", ", PluginLoader.PluginsMetadata));
            PluginComponent.Create();
        }
    }
}