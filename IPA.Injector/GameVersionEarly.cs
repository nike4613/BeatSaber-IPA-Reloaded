#nullable enable
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
                var streamLength = stream.Length;

                while (stream.Position < streamLength && pos < key.Length)
                {
                    if (reader.ReadByte() == key[pos]) pos++;
                    else pos = 0;
                }

                if (stream.Position == streamLength) // we went through the entire stream without finding the key
                    throw new KeyNotFoundException("Could not find key '" + key + "' in " + mgr);

                var startIndex = 0L;
                var endIndex = 0L;

                while (stream.Position < streamLength && endIndex == 0L)
                {
                    var current = (char)reader.ReadByte();
                    if (char.IsDigit(current))
                    {
                        startIndex = stream.Position - 1;
                        var dotCount = 0;

                        while (stream.Position < streamLength)
                        {
                            current = (char)reader.ReadByte();
                            if (char.IsDigit(current))
                            {
                                if (dotCount == 2 && stream.Position < streamLength && !char.IsDigit((char)reader.PeekChar()))
                                {
                                    endIndex = stream.Position;
                                    break;
                                }
                            }
                            else if (current == '.')
                            {
                                dotCount++;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                var strlen = (int)(endIndex - startIndex);
                _ = stream.Seek(-strlen, SeekOrigin.Current);
                var strbytes = reader.ReadBytes(strlen);

                return Encoding.UTF8.GetString(strbytes);
            }
        }

        internal static AlmostVersion SafeParseVersion() => new(GetGameVersion());

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

        internal static readonly char[] IllegalCharacters = new char[]
            {
                '<', '>', ':', '/', '\\', '|', '?', '*', '"',
                '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
                '\u0008', '\u0009', '\u000a', '\u000b', '\u000c', '\u000d', '\u000e', '\u000d',
                '\u000f', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016',
                '\u0017', '\u0018', '\u0019', '\u001a', '\u001b', '\u001c', '\u001d', '\u001f',
            };
    }
}
