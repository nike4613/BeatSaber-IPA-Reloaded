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

                var bootstrapper = new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
                bootstrapper.Destroyed += Bootstrapper_Destroyed;
            }
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
