using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace IllusionInjector.Updating
{
    /** // JSON format
     * {
     *   "_updateScript": "0.1",            // version
     *   "<pluginName>": {                  // an entry for your plugin, using its annotated name
     *     "version": "<version>",          // required, should be in .NET Version class format
     *                                      // note: only required if neither newName nor newScript is specified
     *     "newName": "<newName>",          // optional, defines a new name for the plugin (gets saved under this name) 
     *                                      // (updater will also check this file for this name to get latest)
     *     "newScript": "<newScript>",      // optional, defines a new location for the update script
     *                                      // updater will look here for latest version too
     *                                      // note: if both newName and newScript are defined, the updater will only look in newScript
     *                                      //       for newName, and not any other combination
     *     "download": "<url>",             // required, defines URL to use for downloading new version
     *                                      // note: only required if neither newName nor newScript is specified
     *   },
     *   ...
     * }
     */

    class UpdateScript
    {
        static readonly Version ScriptVersion = new Version(0, 1);

        public Version Version { get; private set; }

        private Dictionary<string, PluginVersionInfo> info = new Dictionary<string, PluginVersionInfo>();
        public IReadOnlyDictionary<string, PluginVersionInfo> Info { get => info; }

        public class PluginVersionInfo
        {
            public Version Version { get; protected internal set; }
            public string NewName { get; protected internal set; }
            public Uri NewScript { get; protected internal set; }
            public Uri Download { get; protected internal set; }
        }

        public static UpdateScript Parse(JSONObject jscript)
        {
            var script = new UpdateScript
            {
                Version = Version.Parse(jscript["_updateScript"].Value)
            };
            if (script.Version != ScriptVersion)
                throw new UpdateScriptParseException("Script version mismatch");

            jscript.Remove("_updateScript");

            foreach (var kvp in jscript)
            {
                var obj = kvp.Value.AsObject;
                var pvi = new PluginVersionInfo
                {
                    Version = obj.Linq.Any(p => p.Key == "version") ? Version.Parse(obj["version"].Value) : null,
                    Download = obj.Linq.Any(p => p.Key == "download") ? new Uri(obj["download"].Value) : null,

                    NewName = obj.Linq.Any(p => p.Key == "newName") ? obj["newName"] : null,
                    NewScript = obj.Linq.Any(p => p.Key == "newScript") ? new Uri(obj["newScript"]) : null
                };
                if (pvi.NewName == null && pvi.NewScript == null && (pvi.Version == null || pvi.Download == null))
                    throw new UpdateScriptParseException($"Required fields missing from object {kvp.Key}");

                script.info.Add(kvp.Key, pvi);
            }

            return script;
        }

        [Serializable]
        private class UpdateScriptParseException : Exception
        {
            public UpdateScriptParseException()
            {
            }

            public UpdateScriptParseException(string message) : base(message)
            {
            }

            public UpdateScriptParseException(string message, Exception innerException) : base(message, innerException)
            {
            }

            protected UpdateScriptParseException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
