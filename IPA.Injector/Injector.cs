using IPA.Loader;
using IPA.Logging;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static IPA.Logging.Logger;
using Logger = IPA.Logging.Logger;

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
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;
                var bootstrapper = new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
                bootstrapper.Destroyed += Bootstrapper_Destroyed;
            }
        }

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
            if (Logger.LogCreated)
                AssemblyLibLoaderCallLogger(lvl, message);
            else
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                    Console.WriteLine($"[{lvl}] {message}");
        }
        private static void AssemblyLibLoaderCallLogger(Level lvl, string message)
        {
            Logger.log.Log(lvl, message);
        }

        private static void Bootstrapper_Destroyed()
        {
            PluginComponent.Create();
        }
    }
}
