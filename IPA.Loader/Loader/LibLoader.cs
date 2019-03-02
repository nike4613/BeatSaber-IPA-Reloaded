using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IPA.Logging;
using Mono.Cecil;

namespace IPA.Loader
{
    internal class CecilLibLoader : BaseAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            LibLoader.SetupAssemblyFilenames();

            var testFile = $"{name.Name}.{name.Version}.dll";

            if (LibLoader.FilenameLocations.TryGetValue(testFile, out string path))
            {
                if (File.Exists(path))
                {
                    return AssemblyDefinition.ReadAssembly(path, parameters);
                }
            }

            return base.Resolve(name, parameters);
        }
    }

    internal static class LibLoader
    {
        internal static string LibraryPath => Path.Combine(Environment.CurrentDirectory, "Libs");
        internal static string NativeLibraryPath => Path.Combine(LibraryPath, "Native");
        internal static Dictionary<string, string> FilenameLocations;

        internal static void SetupAssemblyFilenames()
        {
            if (FilenameLocations == null)
            {
                FilenameLocations = new Dictionary<string, string>();

                foreach (var fn in TraverseTree(LibraryPath, s => s != NativeLibraryPath))
                    if (FilenameLocations.ContainsKey(fn.Name))
                        Log(Logger.Level.Critical, $"Multiple instances of {fn.Name} exist in Libs! Ignoring {fn.FullName}");
                    else FilenameLocations.Add(fn.Name, fn.FullName);
            }
        }

        public static Assembly AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            var asmName = new AssemblyName(e.Name);
            Log(Logger.Level.Debug, $"Resolving library {asmName}");

            SetupAssemblyFilenames();

            var testFile = $"{asmName.Name}.{asmName.Version}.dll";
            Log(Logger.Level.Debug, $"Looking for file {testFile}");

            if (FilenameLocations.TryGetValue(testFile, out string path))
            {
                Log(Logger.Level.Debug, $"Found file {testFile} as {path}");
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }

                Log(Logger.Level.Critical, $"but {path} no longer exists!");
            }
            
            Log(Logger.Level.Critical, $"No library {asmName} found");

            return null;
        }

        internal static void Log(Logger.Level lvl, string message)
        { // multiple proxy methods to delay loading of assemblies until it's done
            if (Logger.LogCreated)
                AssemblyLibLoaderCallLogger(lvl, message);
            else
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                    Console.WriteLine($"[{lvl}] {message}");
        }

        private static void AssemblyLibLoaderCallLogger(Logger.Level lvl, string message)
        {
            Logger.libLoader.Log(lvl, message);
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-iterate-through-a-directory-tree
        private static IEnumerable<FileInfo> TraverseTree(string root, Func<string, bool> dirValidator = null)
        {
            if (dirValidator == null) dirValidator = s => true;

            // Data structure to hold names of subfolders to be
            // examined for files.
            Stack<string> dirs = new Stack<string>(32);

            if (!Directory.Exists(root))
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
                    subDirs = Directory.GetDirectories(currentDir);
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
                catch (UnauthorizedAccessException)
                {
                    //Console.WriteLine(e.Message);
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    //Console.WriteLine(e.Message);
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(currentDir);
                }

                catch (UnauthorizedAccessException)
                {

                    //Console.WriteLine(e.Message);
                    continue;
                }

                catch (DirectoryNotFoundException)
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
                    FileInfo nextValue;
                    try
                    {
                        // Perform whatever action is required in your scenario.
                        nextValue = new FileInfo(file);
                        //Console.WriteLine("{0}: {1}, {2}", fi.Name, fi.Length, fi.CreationTime);
                    }
                    catch (FileNotFoundException)
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
