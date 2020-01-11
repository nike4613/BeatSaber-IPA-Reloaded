using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
#endif

namespace IPA.Injector
{
    internal static class GameVersionEarly
    {
        internal static string ResolveDataPath(string installDir) =>
            Directory.EnumerateDirectories(installDir, "*_Data").First();

        internal static string GlobalGameManagers(string installDir) =>
            Path.Combine(ResolveDataPath(installDir), "globalgamemanagers");

        internal static string GetGameVersion()
        {
            var mgr = GlobalGameManagers(UnityGame.InstallPath);

            using (var stream = File.OpenRead(mgr))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                const string key = "public.app-category.games";
                int pos = 0;

                while (stream.Position < stream.Length && pos < key.Length)
                {
                    if (reader.ReadByte() == key[pos]) pos++;
                    else pos = 0;
                }

                if (stream.Position == stream.Length) // we went through the entire stream without finding the key
                    throw new KeyNotFoundException("Could not find key '" + key + "' in " + mgr);

                // otherwise pos == key.Length, which means we found it
                int offset = 136 - key.Length - sizeof(int);
                stream.Seek(offset, SeekOrigin.Current); // advance past junk to beginning of string

                int strlen = reader.ReadInt32(); // assumes LE
                var strbytes = reader.ReadBytes(strlen);

                return Encoding.UTF8.GetString(strbytes);
            }
        }

        internal static SemVer.Version SafeParseVersion() => new SemVer.Version(GetGameVersion(), true);

        private static void _Load() 
        {
            UnityGame.SetEarlyGameVersion(SafeParseVersion());
            UnityGame.CheckGameVersionBoundary();
        }

        internal static void Load()
        {
            // This exists for the same reason the wierdness in Injector.Main does
            _ = Type.GetType("SemVer.Version, SemVer", false);

            _Load();
        }
    }
}
