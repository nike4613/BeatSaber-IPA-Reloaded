#nullable enable
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace IPA.Injector
{
    internal static class GameVersionEarly
    {
        private const string GlobalGameManagersFileName = "globalgamemanagers";
        private const string AppStoreCategory = "public.app-category.games";

        internal static string ResolveDataPath(string installDir) =>
            Directory.EnumerateDirectories(installDir, "*_Data").First();

        private static string GetGameVersion()
        {
            var filePath = Path.Combine(ResolveDataPath(UnityGame.InstallPath), GlobalGameManagersFileName);
            using var fileStream = File.OpenRead(filePath);
            using var binaryReader = new BinaryReader(fileStream, Encoding.UTF8);

            if (!TrySeekToKey(binaryReader, AppStoreCategory))
            {
                throw new KeyNotFoundException($"Could not find key '{AppStoreCategory}' in {GlobalGameManagersFileName}");
            }

            if (!TryFindVersion(binaryReader, out var gameVersion))
            {
                throw new InvalidDataException($"Could not find a valid game version string in {GlobalGameManagersFileName}");
            }

            return gameVersion;
        }

        private static bool TrySeekToKey(BinaryReader reader, string key)
        {
            var position = 0;
            var stream = reader.BaseStream;
            while (stream.Position < stream.Length && position < key.Length)
            {
                if (reader.ReadByte() == key[position])
                {
                    position++;
                }
                else
                {
                    position = 0;
                }
            }

            return stream.Position != stream.Length;
        }

        private static bool TryFindVersion(BinaryReader reader, [MaybeNullWhen(false)] out string version)
        {
            var stream = reader.BaseStream;
            while (stream.Position < stream.Length)
            {
                var current = (char)reader.ReadByte();

                if (!char.IsDigit(current))
                {
                    continue;
                }

                var bytesRead = 1;
                var dotCount = 0;

                while (stream.Position < stream.Length)
                {
                    current = (char)reader.ReadByte();
                    bytesRead++;

                    if (current == '.')
                    {
                        dotCount++;
                    }
                    else if (!char.IsDigit(current))
                    {
                        break;
                    }

                    // Ensures we get a valid SemVer.
                    if (dotCount == 2)
                    {
                        stream.Seek(-bytesRead - sizeof(int), SeekOrigin.Current);
                        var lengthPrefix = reader.ReadInt32();
                        version = Encoding.UTF8.GetString(reader.ReadBytes(lengthPrefix));

                        return true;
                    }
                }
            }

            version = null;
            return false;
        }

        private static AlmostVersion SafeParseVersion() => new(GetGameVersion());

        private static void _Load()
        {
            UnityGame.SetEarlyGameVersion(SafeParseVersion());
            UnityGame.CheckGameVersionBoundary();
        }

        internal static void Load()
        {
            // This exists for the same reason the weirdness in Injector.Main does
            _ = Type.GetType("SemVer.Version, SemVer", false);

            _Load();
        }
    }
}
