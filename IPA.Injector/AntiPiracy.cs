using System;
using System.IO;
using IPA.Utilities;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
#endif

namespace IPA.Injector
{
    internal class AntiPiracy
    {
        public static bool IsInvalid(string path)
        {
            var dataPlugins = Path.Combine(GameVersionEarly.ResolveDataPath(path), "Plugins");

            return 
                File.Exists(Path.Combine(path, "IGG-GAMES.COM.url")) ||
                File.Exists(Path.Combine(path, "SmartSteamEmu.ini")) ||
                File.Exists(Path.Combine(path, "GAMESTORRENT.CO.url")) ||
                File.Exists(Path.Combine(dataPlugins, "BSteam crack.dll")) ||
                File.Exists(Path.Combine(dataPlugins, "HUHUVR_steam_api64.dll")) ||
                Directory.GetFiles(dataPlugins, "*.ini", SearchOption.TopDirectoryOnly).Length > 0;
        }
    }
}
