using System;
using System.IO;

namespace IPA.Loader.Features
{
    internal class ConfigProviderFeature : Feature
    {
        public override bool Initialize(PluginMetadata meta, string[] parameters)
        {// parameters should be (fully qualified name of provider type)
            if (parameters.Length != 1)
            {
                InvalidMessage = "Incorrect number of parameters";
                return false;
            }

            RequireLoaded(meta);

            Type getType;
            try
            {
                getType = meta.Assembly.GetType(parameters[0]);
            }
            catch (ArgumentException)
            {
                InvalidMessage = $"Invalid type name {parameters[0]}";
                return false;
            }
            catch (Exception e) when (e is FileNotFoundException || e is FileLoadException || e is BadImageFormatException)
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
