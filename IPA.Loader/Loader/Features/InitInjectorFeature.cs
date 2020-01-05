using System;
using System.IO;
using System.Reflection;
#if NET3
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Loader.Features
{
    internal class InitInjectorFeature : Feature
    {
        protected internal override bool StoreOnPlugin => false;

        public override bool Initialize(PluginMetadata meta, string[] parameters)
        { // parameters should be (assembly qualified lookup type, [fully qualified type]:[method name])
          // method should be static
            if (parameters.Length != 2)
            {
                InvalidMessage = "Incorrect number of parameters";
                return false;
            }

            RequireLoaded(meta);

            var methodParts = parameters[1].Split(':');

            var type = Type.GetType(parameters[0], false);
            if (type == null)
            {
                InvalidMessage = $"Could not find type {parameters[0]}";
                return false;
            }

            Type getType;
            try
            {
                getType = meta.Assembly.GetType(methodParts[0]);
            }
            catch (ArgumentException)
            {
                InvalidMessage = $"Invalid type name {methodParts[0]}";
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

            MethodInfo method;
            try
            {
                method = getType.GetMethod(methodParts[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                  null, new[]
                                  {
                                      typeof(object),
                                      typeof(ParameterInfo),
                                      typeof(PluginMetadata)
                                  }, Array.Empty<ParameterModifier>());
            }
            catch (Exception e)
            {
                InvalidMessage = $"Error while loading type: {e}";
                return false;
            }

            if (method == null)
            {
                InvalidMessage = $"Could not find method {methodParts[1]} in type {methodParts[0]}";
                return false;
            }

            try
            {
                var del = (PluginInitInjector.InjectParameter)Delegate.CreateDelegate(typeof(PluginInitInjector.InjectParameter), null, method);
                PluginInitInjector.AddInjector(type, del);
                return true;
            }
            catch (Exception e)
            {
                InvalidMessage = $"Error generated while creating delegate: {e}";
                return false;
            }
        }
    }
}
