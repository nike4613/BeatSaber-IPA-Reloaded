using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using OgDir = System.IO.Directory;

namespace Net3_Proxy
{
    public static class Directory
    {
        public static void Move(string f, string t) => OgDir.Move(f, t);
        public static string[] GetFiles(string d) => OgDir.GetFiles(d);
        public static string[] GetFiles(string d, string s) => OgDir.GetFiles(d, s);
        public static string[] GetFiles(string d, string s, SearchOption o) => OgDir.GetFiles(d, s, o);
        public static string[] GetDirectories(string d) => OgDir.GetDirectories(d);
        public static string[] GetDirectories(string d, string s) => OgDir.GetDirectories(d, s);
        public static string[] GetDirectories(string d, string s, SearchOption o) => OgDir.GetDirectories(d, s, o);
        public static bool Exists(string d) => OgDir.Exists(d);
        public static void Delete(string d) => OgDir.Delete(d);
        public static void Delete(string d, bool r) => OgDir.Delete(d, r);
        public static DirectoryInfo CreateDirectory(string d) => OgDir.CreateDirectory(d);
        public static DirectoryInfo CreateDirectory(string d, DirectorySecurity s) => OgDir.CreateDirectory(d, s);
        public static IEnumerable<string> EnumerateFiles(string d) => GetFiles(d);
        public static IEnumerable<string> EnumerateFiles(string d, string s) => GetFiles(d, s);
        public static IEnumerable<string> EnumerateFiles(string d, string s, SearchOption o) => GetFiles(d, s, o);
        public static IEnumerable<string> EnumerateDirectories(string d) => GetDirectories(d);
        public static IEnumerable<string> EnumerateDirectories(string d, string s) => GetDirectories(d, s);
        public static IEnumerable<string> EnumerateDirectories(string d, string s, SearchOption o) => GetDirectories(d, s, o);
    }
}
