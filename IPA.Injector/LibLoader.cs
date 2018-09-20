using IPA.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static IPA.Logging.Logger;

namespace IPA.Injector
{
    internal class LibLoader
    {
        public static string LibsDir { get; set; } = Path.Combine(Environment.CurrentDirectory, "Libs");
        public static string NativeDir { get; set; } = Path.Combine(LibsDir, "Native");
        private static Dictionary<string, string> filenameLocations = null;

        public static Assembly AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            var asmName = new AssemblyName(e.Name);
            Log(Level.Debug, $"Resolving library {asmName}");

            if (filenameLocations == null)
            {
                filenameLocations = new Dictionary<string, string>();

                foreach (var fn in TraverseTree(LibsDir, s => s != NativeDir))
                    filenameLocations.Add(fn.Name, fn.FullName);
            }

            var testFilen = $"{asmName.Name}.{asmName.Version}.dll";
            Log(Level.Debug, $"Looking for file {testFilen}");

            if (filenameLocations.TryGetValue(testFilen, out string path))
            {
                Log(Level.Debug, $"Found file {testFilen} as {path}");
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
                else
                {
                    Log(Level.Critical, $"but {path} no longer exists!");
                }
            }
            
            Log(Level.Critical, $"No library {asmName} found");

            return null;
        }

        private static void Log(Level lvl, string message)
        { // multiple proxy methods to delay loading of assemblies until it's done
            if (LogCreated)
                AssemblyLibLoaderCallLogger(lvl, message);
            else
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                    Console.WriteLine($"[{lvl}] {message}");
        }

        private static void AssemblyLibLoaderCallLogger(Level lvl, string message)
        {
            libLoader.Log(lvl, message);
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-iterate-through-a-directory-tree
        private static IEnumerable<FileInfo> TraverseTree(string root, Func<string, bool> dirValidator = null)
        {
            if (dirValidator == null) dirValidator = (s) => true;

            // Data structure to hold names of subfolders to be
            // examined for files.
            Stack<string> dirs = new Stack<string>(32);

            if (!System.IO.Directory.Exists(root))
            {
                throw new ArgumentException();
            }
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = System.IO.Directory.GetDirectories(currentDir);
                }
                // An UnauthorizedAccessException exception will be thrown if we do not have
                // discovery permission on a folder or file. It may or may not be acceptable 
                // to ignore the exception and continue enumerating the remaining files and 
                // folders. It is also possible (but unlikely) that a DirectoryNotFound exception 
                // will be raised. This will happen if currentDir has been deleted by
                // another application or thread after our call to Directory.Exists. The 
                // choice of which exceptions to catch depends entirely on the specific task 
                // you are intending to perform and also on how much you know with certainty 
                // about the systems on which this code will run.
                catch (UnauthorizedAccessException e)
                {
                    //Console.WriteLine(e.Message);
                    continue;
                }
                catch (System.IO.DirectoryNotFoundException e)
                {
                    //Console.WriteLine(e.Message);
                    continue;
                }

                string[] files = null;
                try
                {
                    files = System.IO.Directory.GetFiles(currentDir);
                }

                catch (UnauthorizedAccessException e)
                {

                    //Console.WriteLine(e.Message);
                    continue;
                }

                catch (System.IO.DirectoryNotFoundException e)
                {
                    //Console.WriteLine(e.Message);
                    continue;
                }
                
                // Push the subdirectories onto the stack for traversal.
                // This could also be done before handing the files.
                foreach (string str in subDirs)
                    if (dirValidator(str)) dirs.Push(str);

                // Perform the required action on each file here.
                // Modify this block to perform your required task.
                foreach (string file in files)
                {
                    FileInfo nextValue = null;
                    try
                    {
                        // Perform whatever action is required in your scenario.
                        nextValue = new System.IO.FileInfo(file);
                        //Console.WriteLine("{0}: {1}, {2}", fi.Name, fi.Length, fi.CreationTime);
                    }
                    catch (System.IO.FileNotFoundException e)
                    {
                        // If file was deleted by a separate application
                        //  or thread since the call to TraverseTree()
                        // then just continue.
                        //Console.WriteLine(e.Message);
                        continue;
                    }

                    yield return nextValue;
                }
            }
        }
    }
}
