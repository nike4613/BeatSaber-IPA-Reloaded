// BEGIN: section ignore
#nullable enable
using IPA.Logging;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
// END: section ignore
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IPA.Config
{
    internal class SelfConfig
    {
        // This is to allow the doc generation to parse this file and use Newtonsoft to generate a JSON schema
        // BEGIN: section ignore

        public static Config LoaderConfig { get; set; } = null!; // this is set before used

        public static SelfConfig Instance = new();

        public static void Load()
        {
            LoaderConfig = Config.GetConfigFor(IPAName, "json");
            Instance = LoaderConfig.Generated<SelfConfig>();
            Instance.OnReload();
        }

        protected virtual void CopyFrom(SelfConfig cfg) { }
        protected internal virtual void OnReload()
        {
            if (Regenerate)
                CopyFrom(new SelfConfig { Regenerate = false });
            StandardLogger.Configure();
        }

        protected internal virtual void Changed()
        {
            Logger.Default.Debug("SelfConfig Changed called");
        }

        public static void ReadCommandLine(string[] args)
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "--debug":
                    case "--mono-debug":
                        CommandLineValues.Debug.ShowDebug = true;
                        CommandLineValues.Debug.ShowCallSource = true;
                        break;
                    case "--no-yeet":
                        CommandLineValues.YeetMods = false;
                        break;
                    case "--no-logs":
                        CommandLineValues.WriteLogs = false;
                        break;
                    case "--darken-message":
                        CommandLineValues.Debug.DarkenMessages = true;
                        break;
                    case "--condense-logs":
                        CommandLineValues.Debug.CondenseModLogs = true;
                        break;
                    case "--plugin-logs":
                        CommandLineValues.Debug.CreateModLogs = true;
                        break;
#if false
                    case "--no-updates":
                        CommandLineValues.Updates.AutoCheckUpdates = false;
                        CommandLineValues.Updates.AutoUpdate = false;
                        break;
#endif
                    case "--trace":
                        CommandLineValues.Debug.ShowTrace = true;
                        break;
                }
            }
        }

        public void CheckVersionBoundary()
        {
            if (ResetGameAssebliesOnVersionChange && Utilities.UnityGame.IsGameVersionBoundary)
            {
                GameAssemblies = GetDefaultGameAssemblies();
            }
        }

        internal const string IPAName = "Beat Saber IPA";
        internal const string IPAVersion = "4.2.2.0";

        // uses Updates.AutoUpdate, Updates.AutoCheckUpdates, YeetMods, Debug.ShowCallSource, Debug.ShowDebug, 
        //      Debug.CondenseModLogs
        internal static SelfConfig CommandLineValues = new();

        // For readability's sake, I want the default values to be visible in source.
#pragma warning disable CA1805 // Do not initialize unnecessarily

        // END: section ignore

        public virtual bool Regenerate { get; set; } = true;

#if false
        public class Updates_
        {
            public virtual bool AutoUpdate { get; set; } = true;
            // LINE: ignore 2
            public static bool AutoUpdate_ => (Instance?.Updates?.AutoUpdate ?? true)
                                           &&   CommandLineValues.Updates.AutoUpdate;

            public virtual bool AutoCheckUpdates { get; set; } = true;
            // LINE: ignore 2
            public static bool AutoCheckUpdates_ => (Instance?.Updates?.AutoCheckUpdates ?? true)
                                                 &&   CommandLineValues.Updates.AutoCheckUpdates;
        }

        // LINE: ignore
        [NonNullable]
        public virtual Updates_ Updates { get; set; } = new Updates_();
#endif

        public class Debug_
        {
            public virtual bool ShowCallSource { get; set; } = false;
            // LINE: ignore 2
            public static bool ShowCallSource_ => (Instance?.Debug?.ShowCallSource ?? false)
                                               ||   CommandLineValues.Debug.ShowCallSource;

            public virtual bool ShowDebug { get; set; } = false;
            // LINE: ignore 2
            public static bool ShowDebug_ => (Instance?.Debug?.ShowDebug ?? false)
                                          ||   CommandLineValues.Debug.ShowDebug;

            // This option only takes effect after a full game restart, unless new logs are created again
            public virtual bool CondenseModLogs { get; set; } = false;
            // LINE: ignore 2
            public static bool CondenseModLogs_ => (Instance?.Debug?.CondenseModLogs ?? false)
                                                ||   CommandLineValues.Debug.CondenseModLogs;

            // This option only takes effect after a full game restart, unless new logs are created again
            public virtual bool CreateModLogs { get; set; } = false;
            // LINE: ignore 2
            public static bool CreateModLogs_ => (Instance?.Debug?.CreateModLogs ?? false)
                                              ||    CommandLineValues.Debug.CreateModLogs;

            public virtual bool ShowHandledErrorStackTraces { get; set; } = false;
            // LINE: ignore
            public static bool ShowHandledErrorStackTraces_ => Instance?.Debug?.ShowHandledErrorStackTraces ?? false;

            public virtual bool HideMessagesForPerformance { get; set; } = true;
            // LINE: ignore
            public static bool HideMessagesForPerformance_ => Instance?.Debug?.HideMessagesForPerformance ?? true;

            public virtual int HideLogThreshold { get; set; } = 512;
            // LINE: ignore
            public static int HideLogThreshold_ => Instance?.Debug?.HideLogThreshold ?? 512;

            public virtual bool ShowTrace { get; set; } = false;
            // LINE: ignore 2
            public static bool ShowTrace_ => (Instance?.Debug?.ShowTrace ?? false)
                                          ||   CommandLineValues.Debug.ShowTrace;
            public virtual bool SyncLogging { get; set; } = false;
            // LINE: ignore
            public static bool SyncLogging_ => Instance?.Debug?.SyncLogging ?? false;

            public virtual bool DarkenMessages { get; set; } = false;
            // LINE: ignore 2
            public static bool DarkenMessages_ => (Instance?.Debug?.DarkenMessages ?? false)
                                               || CommandLineValues.Debug.DarkenMessages;
        }

        // LINE: ignore
        [NonNullable]
        public virtual Debug_ Debug { get; set; } = new();

        public class AntiMalware_
        {
            public virtual bool UseIfAvailable { get; set; } = true;
            // LINE: ignore
            public static bool UseIfAvailable_ => Instance?.AntiMalware?.UseIfAvailable ?? true;

            public virtual bool RunPartialThreatCode { get; set; } = false;
            // LINE: ignore
            public static bool RunPartialThreatCode_ => Instance?.AntiMalware?.RunPartialThreatCode ?? true;
        }

        // LINE: ignore
        [NonNullable]
        public virtual AntiMalware_ AntiMalware { get; set; } = new();

        public virtual bool YeetMods { get; set; } = true;
        // LINE: ignore 2
        public static bool YeetMods_ => (Instance?.YeetMods ?? true)
                                     &&   CommandLineValues.YeetMods;

        [JsonIgnore]
        public bool WriteLogs { get; set; } = true;

        public virtual bool ResetGameAssebliesOnVersionChange { get; set; } = true;

        // LINE: ignore
        [NonNullable, UseConverter(typeof(CollectionConverter<string, HashSet<string?>>))]
        public virtual HashSet<string> GameAssemblies { get; set; } = GetDefaultGameAssemblies();

        // BEGIN: section ignore
        public static HashSet<string> GetDefaultGameAssemblies()
            => new()
            {
#if BeatSaber // provide these defaults only for Beat Saber builds
                "Main.dll", "Core.dll", "HMLib.dll", "HMUI.dll", "HMRendering.dll", "VRUI.dll",
                "BeatmapCore.dll", "GameplayCore.dll","HMLibAttributes.dll", 
#else // otherwise specify Assembly-CSharp.dll
                "Assembly-CSharp.dll"
#endif
            };
        // END: section ignore

        // LINE: ignore
#if false // used to make schema gen happy
        private static HashSet<string> GetDefaultGameAssemblies() => null;
        // LINE: ignore
#endif

        // LINE: ignore
        public static HashSet<string> GameAssemblies_ => Instance?.GameAssemblies ?? new HashSet<string> { "Assembly-CSharp.dll" };

        // LINE: ignore
#if false // Used for documentation schema generation
        [JsonProperty(Required = Required.DisallowNull)]
        public virtual string LastGameVersion { get; set; } = null;
        // LINE: ignore 2
#endif
        public virtual string? LastGameVersion { get; set; } = null;

        // LINE: ignore
        public static string? LastGameVersion_ => Instance?.LastGameVersion;
    }
}