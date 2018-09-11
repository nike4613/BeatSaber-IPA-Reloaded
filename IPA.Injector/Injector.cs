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
        private static bool injected = false;
        public static void Inject()
        {
            if (!injected)
            {
                injected = true;

                #region Add Library load locations
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;
                try
                {
                    if (!SetDllDirectory(Path.Combine(Environment.CurrentDirectory, "Libs", "Native")))
                    {
                        libLoader.Warn("Unable to add native library path to load path");
                    }
                }
                catch (Exception) { }
                #endregion

                var bootstrapper = new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
                bootstrapper.Destroyed += Bootstrapper_Destroyed;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetDllDirectory(string lpPathName);

        #region Managed library loader
        private static string libsDir;
        private static Assembly AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            if (libsDir == null)
                libsDir = Path.Combine(Environment.CurrentDirectory, "Libs");

            var asmName = new AssemblyName(e.Name);
            Log(Level.Debug, $"Resolving library {asmName}");

            var testFilen = Path.Combine(libsDir, $"{asmName.Name}.{asmName.Version}.dll");
            Log(Level.Debug, $"Looking for file {testFilen}");

            if (File.Exists(testFilen))
                return Assembly.LoadFile(testFilen);

            Log(Level.Critical, $"Could not load library {asmName}");

            return null;
        }
        private static void Log(Level lvl, string message)
        { // multiple proxy methods to delay loading of assemblies until it's done
            if (LogCreated)
                AssemblyLibLoaderCallLogger(lvl, message);
            else
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                    Console.WriteLine($"[{lvl}] {message}");
        }
        private static void AssemblyLibLoaderCallLogger(Level lvl, string message)
        {
            libLoader.Log(lvl, message);
        }
        #endregion

        private static void Bootstrapper_Destroyed()
        {
            PluginComponent.Create();
        }
    }
}
