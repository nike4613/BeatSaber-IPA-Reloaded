using Harmony;
using IPA.Loader;
using IPA.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static IPA.Logging.Logger;

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

            // This loads AppDomain, System.IO, System.Collections.Generic, and System.Reflection.
            // If kernel32.dll is not already loaded, this will also load it.
            // This call also loads IPA.Loader and initializes the logging system. In the process
            // it loads Ionic.Zip.
            SetupLibraryLoading();

            // This loads System.Runtime.InteropServices, and Microsoft.Win32.SafeHandles.
            Windows.WinConsole.Initialize();

            // This will load Harmony and UnityEngine.CoreModule
            InstallBootstrapPatch();
        }

        private static void InstallBootstrapPatch()
        {
            var harmony = HarmonyInstance.Create("IPA_NonDestructive_Bootstrapper");
            // patch the Application static constructor to create the bootstrapper after being called
            harmony.Patch(typeof(Application).TypeInitializer, null, new HarmonyMethod(typeof(Injector).GetMethod(nameof(CreateBootstrapper), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void CreateBootstrapper()
        {
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
