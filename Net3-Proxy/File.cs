using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using OgFile = System.IO.File;

namespace Net3_Proxy
{
    public static class File
    {
        public static string ReadAllText(string fn) => OgFile.ReadAllText(fn);
        public static string ReadAllText(string fn, Encoding enc) => OgFile.ReadAllText(fn, enc);
        public static bool Exists(string fn) => OgFile.Exists(fn);
        public static void Delete(string fn) => OgFile.Delete(fn);
        public static void AppendAllLines(string path, IEnumerable<string> contents)
        {
            Path.Validate(path);
            if (contents == null)
                return;
            using (TextWriter textWriter = new StreamWriter(path, true))
                foreach (string value in contents)
                    textWriter.WriteLine(value);
        }
        public static string[] ReadAllLines(string p) => OgFile.ReadAllLines(p);
        public static FileStream OpenRead(string p) => OgFile.OpenRead(p);
    }
}
