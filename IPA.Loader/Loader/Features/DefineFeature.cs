#nullable enable
using IPA.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace IPA.Loader.Features
{
    internal class DefineFeature : Feature
    {
        public static bool NewFeature = true;

        private class DataModel
        {
            [JsonPropertyName("type")]
            [JsonRequired]
            public string TypeName { get; init; } = "";

            [JsonPropertyName("name")]
            // TODO: Originally DisallowNull
            public string? ActualName { get; init; }

            public string Name => ActualName ?? TypeName;
        }

        private DataModel data = null!;

        protected override bool Initialize(PluginMetadata meta, JsonObject featureData)
        {
            Logger.Features.Debug("Executing DefineFeature Init");

            try
            {
                data = featureData.Deserialize<DataModel>() ?? throw new InvalidOperationException("Feature data is null");
            }
            catch (Exception e)
            {
                InvalidMessage = $"Invalid data: {e}";
                return false;
            }

            InvalidMessage = $"Feature {data.Name} already exists";
            return PreregisterFeature(meta, data.Name);
        }

        public override void BeforeInit(PluginMetadata meta)
        {
            Logger.Features.Debug("Executing DefineFeature AfterInit");

            Type type;
            try
            {
                type = meta.Assembly.GetType(data.TypeName);
            }
            catch (ArgumentException)
            {
                Logger.Features.Error($"Invalid type name {data.TypeName}");
                return;
            }
            catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                var filename = "";

                switch (e)
                {
                    case FileNotFoundException fn:
                        filename = fn.FileName;
                        break;
                    case FileLoadException fl:
                        filename = fl.FileName;
                        break;
                    case BadImageFormatException bi:
                        filename = bi.FileName;
                        break;
                }

                Logger.Features.Error($"Could not find {filename} while loading type");
                return;
            }

            if (type == null)
            {
                Logger.Features.Error($"Invalid type name {data.TypeName}");
                return;
            }

            try
            {
                if (RegisterFeature(meta, data.Name, type))
                {
                    NewFeature = true;
                    return;
                }

                Logger.Features.Error($"Feature with name {data.Name} already exists");
                return;
            }
            catch (ArgumentException)
            {
                Logger.Features.Error($"{type.FullName} not a subclass of {nameof(Feature)}");
                return;
            }
        }
    }
}
