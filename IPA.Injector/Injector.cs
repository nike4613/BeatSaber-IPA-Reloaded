#nullable enable
using IPA.AntiMalware;
using IPA.Config;
using IPA.Loader;
using IPA.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using static IPA.Logging.Logger;

namespace IPA.Injector
{
    /// <summary>
    /// The entry point type for BSIPA's Doorstop injector.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    internal static class Injector
    {
        internal static void Inject()
        { // entry point for doorstop
          // At this point, literally nothing but mscorlib is loaded,
          // and since this class doesn't have any static fields that
          // aren't defined in mscorlib, we can control exactly what
          // gets loaded.
            try
            {
                var args = Environment.GetCommandLineArgs();
                CommandLineParser.ParseArguments(args);

                Loader.LibLoader.Configure();

                Default.Debug("Initializing logger");

                if (AntiPiracy.IsInvalid(Environment.CurrentDirectory))
                {
                    Default.Error("Invalid installation; please buy the game to run BSIPA.");

                    return;
                }

                WriteDebugLogs(args);

                EnsureDirectories();

                SelfConfig.Load();
                DisabledConfig.Load();

                Logging.Logger.Injector.Debug("Prepping bootstrapper");

                Bootstrap.InstallPatch();

                GameVersionEarly.Load();

                AntiMalwareEngine.Initialize();

                Bootstrap.LoadPlugins();
            }
            catch (Exception e)
            {
                Log(LoggerId.Injector, Level.Critical, e.ToString());
            }
        }

        private static void WriteDebugLogs(string[] args)
        {
            var runtime = OpenXRHelper.GetOpenXRRuntime();
            Default.Debug($"Command-line: \"{string.Join(" ", args)}\"");
            Default.Debug($"Current time: {DateTimeOffset.Now}");
            Default.Debug($"Current culture: {CultureInfo.CurrentCulture.Name}");
            Default.Debug($"Current OpenXR runtime: {runtime ?? "null"}, exists: {File.Exists(runtime)}");
        }

        private static void EnsureDirectories()
        {
            string path;
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "UserData")))
                _ = Directory.CreateDirectory(path);
            if (!Directory.Exists(path = Path.Combine(Environment.CurrentDirectory, "Plugins")))
                _ = Directory.CreateDirectory(path);
        }
    }
}
