using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Utilities
{
    /// <summary>
    /// A class providing static utility functions that in any other language would just *exist*.
    /// </summary>
    public static class LoneFunctions
    {
        /// <summary>
        /// Converts a hex string to a byte array.
        /// </summary>
        /// <param name="hex">the hex stream</param>
        /// <returns>the corresponding byte array</returns>
        public static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ba"></param>
        /// <returns></returns>
        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        // Copyright (c) 2008-2013 Hafthor Stefansson
        // Distributed under the MIT/X11 software license
        // Ref: http://www.opensource.org/licenses/mit-license.php.
        // From: https://stackoverflow.com/a/8808245/3117125
        /// <summary>
        /// Uses unsafe code to compare 2 byte arrays quickly.
        /// </summary>
        /// <param name="a1">array 1</param>
        /// <param name="a2">array 2</param>
        /// <returns>whether or not they are byte-for-byte equal</returns>
        public static unsafe bool UnsafeCompare(byte[] a1, byte[] a2)
        {
            if (a1 == a2) return true;
            if (a1 == null || a2 == null || a1.Length != a2.Length)
                return false;
            fixed (byte* p1 = a1, p2 = a2)
            {
                byte* x1 = p1, x2 = p2;
                int l = a1.Length;
                for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
                    if (*((long*)x1) != *((long*)x2)) return false;
                if ((l & 4) != 0) { if (*((int*)x1) != *((int*)x2)) return false; x1 += 4; x2 += 4; }
                if ((l & 2) != 0) { if (*((short*)x1) != *((short*)x2)) return false; x1 += 2; x2 += 2; }
                if ((l & 1) != 0) if (*((byte*)x1) != *((byte*)x2)) return false;
                return true;
            }
        }

        /// <summary>
        /// Gets a path relative to the provided folder.
        /// </summary>
        /// <param name="file">the file to relativize</param>
        /// <param name="folder">the source folder</param>
        /// <returns>a path to get from <paramref name="folder"/> to <paramref name="file"/></returns>
        public static string GetRelativePath(string file, string folder)
        {
            Uri pathUri = new Uri(file);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Copies all files from <paramref name="source"/> to <paramref name="target"/>.
        /// </summary>
        /// <param name="source">the source directory</param>
        /// <param name="target">the destination directory</param>
        /// <param name="appendFileName"></param>
        public static void CopyAll(DirectoryInfo source, DirectoryInfo target, string appendFileName = "")
        {
            if (source.FullName.ToLower() == target.FullName.ToLower())
            {
                return;
            }

            // Check if the target directory exists, if not, create it.
            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into it's new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (fi.Name == appendFileName)
                    File.AppendAllLines(Path.Combine(target.ToString(), fi.Name), File.ReadAllLines(fi.FullName));
                else
                    fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir, appendFileName);
            }
        }
    }
}
