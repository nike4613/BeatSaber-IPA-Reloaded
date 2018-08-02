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
        const string ApiBase = "https://www.modsaber.ml/api/";
        const string GetApprovedEndpoint = "public/temp/approved";

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

                foreach (var item in obj["files"].Keys)
                {
                    var key = item as JSONString;
                    if (key.Value == "steam")
                        outp.SteamFile = obj[key.Value]["url"];
                    if (key.Value == "oculus")
                        outp.OculusFile = obj[key.Value]["url"];
                }

                return outp;
            }
        }

    }
}
