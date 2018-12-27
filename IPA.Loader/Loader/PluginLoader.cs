using System;
using System.Reflection;
using Version = SemVer.Version;

namespace IPA.Loader
{
    internal class PluginLoader
    {
        public class PluginMetadata
        {
            public Assembly Assembly;
            public Type PluginType;
            public string Name;
            public Version Version;
        }

        public static void LoadMetadata()
        {

        }
    }
}