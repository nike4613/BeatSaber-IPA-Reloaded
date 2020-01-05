using System;
using System.Linq;
using OgPath = System.IO.Path;

namespace Net3_Proxy
{
    public static class Path
    {
        internal static void Validate(string path)
            => Validate(path, nameof(path));

        internal static void Validate(string path, string parameterName)
        {
            if (path == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (Utils.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is empty");
            }
            if (path.IndexOfAny(OgPath.GetInvalidPathChars()) != -1)
            {
                throw new ArgumentException("Path contains invalid chars");
            }
            if (Environment.OSVersion.Platform < PlatformID.Unix)
            {
                int num = path.IndexOf(':');
                if (num >= 0 && num != 1)
                {
                    throw new ArgumentException(parameterName);
                }
            }
        }

        public static string GetFullPath(string p) => OgPath.GetFullPath(p);
        public static string GetFileNameWithoutExtension(string p) => OgPath.GetFileNameWithoutExtension(p);
        public static string GetFileName(string p) => OgPath.GetFileName(p);
        public static string GetDirectoryName(string p) => OgPath.GetDirectoryName(p);

        public static string Combine(string s) => s;
        public static string Combine(string s, string d) => OgPath.Combine(s, d);
        public static string Combine(string s, string d, string f) => Combine(s, Combine(d, f));
        public static string Combine(string s, string d, string f, string g) => Combine(Combine(s, d), Combine(f, g));
        public static string Combine(params string[] parts)
        {
            if (parts.Length == 0) return "";
            var begin = parts[0];
            foreach (var p in parts.Skip(1))
                begin = Combine(begin, p);
            return begin;
        }
        public static char PathSeparator => OgPath.PathSeparator;
    }
}
