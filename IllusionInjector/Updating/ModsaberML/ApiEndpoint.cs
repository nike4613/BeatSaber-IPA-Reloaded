using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionInjector.Updating.ModsaberML
{
    class ApiEndpoint
    {
#if DEBUG
        public const string ApiBase = "https://www.modsaber.ml/api/";
        public const string GetApprovedEndpoint = "public/temp/approved";
#else
        public const string ApiBase = "https://www.modsaber.ml/api/";
        public const string GetApprovedEndpoint = "public/temp/approved";
#endif

        public class Mod
        {
            public string Name;
            public Version Version;
            public bool Approved;
            public string Title;
            public Version GameVersion;
            public string Author;
            public string SteamFile;
            public string OculusFile;

            public static Mod DecodeJSON(JSONObject obj)
            {
                var outp = new Mod
                {
                    Name = obj["name"],
                    Version = new Version(obj["version"]),
                    Approved = obj["approved"].AsBool,
                    Title = obj["title"],
                    GameVersion = new Version(obj["gameVersion"]),
                    Author = obj["author"]
                };

                foreach (var item in obj["files"])
                {
                    var key = item.Key;
                    if (key == "steam")
                        outp.SteamFile = item.Value["url"];
                    if (key == "oculus")
                        outp.OculusFile = item.Value["url"];
                }

                return outp;
            }

            public override string ToString()
            {
                return $"{{\"{Title} ({Name})\"v{Version} for {GameVersion} by {Author} with \"{SteamFile}\" and \"{OculusFile}\"}}";
            }
        }

    }
}
