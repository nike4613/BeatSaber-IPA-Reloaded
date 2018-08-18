using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace IllusionInjector
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
            //Logger.log.Debug($"Resolving library {asmName}");

            var testFilen = Path.Combine(libsDir, $"{asmName.Name}.{asmName.Version}.dll");
            //Logger.log.Debug($"Looking for file {testFilen}");

            if (File.Exists(testFilen))
            {
                return Assembly.LoadFile(testFilen);
            }

            //Logger.log.Error($"Could not load library {asmName}");

            return null;
        }

        private static void Bootstrapper_Destroyed()
        {
            PluginComponent.Create();
        }
    }
}
