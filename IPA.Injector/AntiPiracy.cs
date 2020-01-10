using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
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

            try
            {
                var userDir = GetPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"),
                                               KnownFolderFlags.AliasOnly | KnownFolderFlags.DontVerify);
                var userDir2 = GetPath(new Guid("7d83ee9b-2244-4e70-b1f5-5393042af1e4"),
                                               KnownFolderFlags.AliasOnly | KnownFolderFlags.DontVerify);

                var curdir = Environment.CurrentDirectory;

                if (curdir.IsSubPathOf(userDir) ||
                    curdir.IsSubPathOf(userDir2)) return false;
            }
            catch { }

            // To the guys that maintain a fork that removes this code: I would greatly appreciate if we could talk
            //   about this for a little bit. Please message me on Discord at DaNike#6223
            return 
                File.Exists(Path.Combine(path, "IGG-GAMES.COM.url")) ||
                File.Exists(Path.Combine(path, "SmartSteamEmu.ini")) ||
                File.Exists(Path.Combine(path, "GAMESTORRENT.CO.url")) ||
                File.Exists(Path.Combine(dataPlugins, "BSteam crack.dll")) ||
                File.Exists(Path.Combine(dataPlugins, "HUHUVR_steam_api64.dll")) ||
                Directory.GetFiles(dataPlugins, "*.ini", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static string GetPath(Guid guid, KnownFolderFlags flags)
        {
            int result = SHGetKnownFolderPath(guid, (uint)flags, WindowsIdentity.GetCurrent().Token, out IntPtr outPath);
            if (result >= 0)
            {
                string path = Marshal.PtrToStringUni(outPath);
                Marshal.FreeCoTaskMem(outPath);
                return path;
            }
            return "";
        }

        /// <summary>
        /// Retrieves the full path of a known folder identified by the folder's known folder ID.
        /// </summary>
        /// <param name="rfid">A known folder ID that identifies the folder.</param>
        /// <param name="dwFlags">Flags that specify special retrieval options. This value can be 0; otherwise, one or
        /// more of the <see cref="KnownFolderFlags"/> values.</param>
        /// <param name="hToken">An access token that represents a particular user. If this parameter is NULL, which is
        /// the most common usage, the function requests the known folder for the current user. Assigning a value of -1
        /// indicates the Default User. The default user profile is duplicated when any new user account is created.
        /// Note that access to the Default User folders requires administrator privileges.</param>
        /// <param name="ppszPath">When this method returns, contains the address of a string that specifies the path of
        /// the known folder. The returned path does not include a trailing backslash.</param>
        /// <returns>Returns S_OK if successful, or an error value otherwise.</returns>
        /// <msdn-id>bb762188</msdn-id>
        [DllImport("Shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)]Guid rfid, uint dwFlags,
            IntPtr hToken, out IntPtr ppszPath);

        /// <summary>
        /// Represents the retrieval options for known folders.
        /// </summary>
        /// <msdn-id>dd378447</msdn-id>
        [Flags]
        private enum KnownFolderFlags : uint
        {
            None = 0x00000000,
            SimpleIDList = 0x00000100,
            NotParentRelative = 0x00000200,
            DefaultPath = 0x00000400,
            Init = 0x00000800,
            NoAlias = 0x00001000,
            DontUnexpand = 0x00002000,
            DontVerify = 0x00004000,
            Create = 0x00008000,
            NoAppcontainerRedirection = 0x00010000,
            AliasOnly = 0x80000000
        }

    }
}
