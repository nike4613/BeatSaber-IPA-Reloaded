#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace IPA.Loader.Features
{
    internal class ConfigProviderFeature : Feature
    {
        private class DataModel
        {
            [JsonProperty("type", Required = Required.Always)]
            public string TypeName = "";
        }

        protected override bool Initialize(PluginMetadata meta, JObject featureData)
        {
            DataModel data;
            try
            {
                data = featureData.ToObject<DataModel>() ?? throw new InvalidOperationException("Feature data is null");
            }
            catch (Exception e)
            {
                InvalidMessage = $"Invalid data: {e}";
                return false;
            }

            Type getType;
            try
            {
                getType = meta.Assembly.GetType(data.TypeName);
            }
            catch (ArgumentException)
            {
                InvalidMessage = $"Invalid type name {data.TypeName}";
                return false;
            }
            catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                string filename;

                switch (e)
                {
                    case FileNotFoundException fn:
                        filename = fn.FileName;
                        goto hasFilename;
                    case FileLoadException fl:
                        filename = fl.FileName;
                        goto hasFilename;
                    case BadImageFormatException bi:
                        filename = bi.FileName;
                    hasFilename:
                        InvalidMessage = $"Could not find {filename} while loading type";
                        break;
                    default:
                        InvalidMessage = $"Error while loading type: {e}";
                        break;
                }

                return false;
            }

            try
            {
                Config.Config.Register(getType);
                return true;
            }
            catch (Exception e)
            {
                InvalidMessage = $"Error while registering config provider: {e}";
                return false;
            }
        }
    }
}
