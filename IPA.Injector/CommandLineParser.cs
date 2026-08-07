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
        public static void ParseArguments(string[] args)
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
                    case "--trace":
                        SelfConfig.CommandLineValues.Debug.ShowTrace = true;
                        break;
                    case "-vrmode":
                        if (i + 1 >= args.Length) continue;
                        OpenXRHelper.SetOpenXRRuntime(args[i + 1]);
                        break;
                }
            }
        }
    }
}
