#nullable enable

using IPA.Config;
using IPA.Logging;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;

namespace IPA.Injector
{
    internal static class CommandLineParser
    {
        public static void ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--verbose":
                        WinConsole.Initialize(i + 1 < args.Length && int.TryParse(args[i + 1], out var processId) ? processId : WinConsole.AttachParent);
                        break;
                    case "--debug":
                    case "--mono-debug":
                        SelfConfig.CommandLineValues.Debug.ShowDebug = true;
                        SelfConfig.CommandLineValues.Debug.ShowCallSource = true;
                        break;
                    case "--no-yeet":
                        SelfConfig.CommandLineValues.YeetMods = false;
                        break;
                    case "--no-logs":
                        SelfConfig.CommandLineValues.WriteLogs = false;
                        break;
                    case "--darken-message":
                        SelfConfig.CommandLineValues.Debug.DarkenMessages = true;
                        break;
                    case "--condense-logs":
                        SelfConfig.CommandLineValues.Debug.CondenseModLogs = true;
                        break;
                    case "--plugin-logs":
                        SelfConfig.CommandLineValues.Debug.CreateModLogs = true;
                        break;
#if false
                    case "--no-updates":
                        CommandLineValues.Updates.AutoCheckUpdates = false;
                        CommandLineValues.Updates.AutoUpdate = false;
                        break;
#endif
                    case "--trace":
                        SelfConfig.CommandLineValues.Debug.ShowTrace = true;
                        break;
                    case "-vrmode":
                        if (i + 1 >= args.Length) continue;
                        SetOpenXRRuntime(args[i + 1]);
                        break;
                }
            }
        }

        private static void SetOpenXRRuntime(string targetRuntime)
        {
            // Forces invalid runtime to prevent OpenXR initialization.
            if (targetRuntime == "none")
            {
                Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", targetRuntime);
                return;
            }

            using var baseKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1\AvailableRuntimes");
            var foundRuntime = baseKey?.GetValueNames().FirstOrDefault(v => File.Exists(v) && Path.GetFileName(v).IndexOf(targetRuntime, StringComparison.Ordinal) >= 0);
            if (foundRuntime != null)
            {
                Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", foundRuntime);
            }
        }
    }
}
