using IllusionInjector.Utilities;
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
        public const string ApiBase = "file://Z:/Users/aaron/Source/Repos/IPA-Reloaded-BeatSaber/IPA.Tests/";
        public const string GetApprovedEndpoint = "updater_test.json";
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

            public class PlatformFile
            {
                public byte[] Hash = new byte[20]; // 20 byte because sha1 is fucky
                public Dictionary<string, byte[]> FileHashes = new Dictionary<string, byte[]>();
                public string DownloadPath = null;
            }

            public PlatformFile SteamFile = null;
            public PlatformFile OculusFile = null;

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
                    var pfile = new PlatformFile()
                    {
                        DownloadPath = item.Value["url"],
                        Hash = LoneFunctions.StringToByteArray(item.Value["hash"])
                    };

                    foreach (var file in item.Value["files"])
                        pfile.FileHashes.Add(file.Key, LoneFunctions.StringToByteArray(file.Value));

                    if (key == "steam")
                        outp.SteamFile = pfile;
                    if (key == "oculus")
                        outp.OculusFile = pfile;
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
