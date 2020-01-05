using System;
using System.IO;

namespace IPA.Loader.Features
{
    internal class DefineFeature : Feature
    {
        public static bool NewFeature = true;

        protected internal override bool StoreOnPlugin => false;

        public override bool Initialize(PluginMetadata meta, string[] parameters)
        { // parameters should be (name, fully qualified type)
            if (parameters.Length != 2)
            {
                InvalidMessage = "Incorrect number of parameters";
                return false;
            }
            
            RequireLoaded(meta);

            Type type;
            try
            {
                type = meta.Assembly.GetType(parameters[1]);
            }
            catch (ArgumentException)
            {
                InvalidMessage = $"Invalid type name {parameters[1]}";
                return false;
            }
            catch (Exception e) when (e is FileNotFoundException || e is FileLoadException || e is BadImageFormatException)
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

                InvalidMessage = $"Could not find {filename} while loading type";
                return false;
            }

            if (type == null)
            {
                InvalidMessage = $"Invalid type name {parameters[1]}";
                return false;
            }

            try
            {
                if (RegisterFeature(parameters[0], type)) return NewFeature = true;

                InvalidMessage = $"Feature with name {parameters[0]} already exists";
                return false;

            }
            catch (ArgumentException)
            {
                InvalidMessage = $"{type.FullName} not a subclass of {nameof(Feature)}";
                return false;
            }
        }
    }
}
