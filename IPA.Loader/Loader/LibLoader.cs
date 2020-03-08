using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using IPA.Logging;
using IPA.Utilities;
using Mono.Cecil;
#if NET3
using Net3_Proxy;
using Directory = Net3_Proxy.Directory;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
#endif

namespace IPA.Loader
{
    internal class CecilLibLoader : BaseAssemblyResolver
    {
        private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        private static readonly string CurrentAssemblyPath = Assembly.GetExecutingAssembly().Location;

        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            LibLoader.SetupAssemblyFilenames();

            if (name.Name == CurrentAssemblyName)
                return AssemblyDefinition.ReadAssembly(CurrentAssemblyPath, parameters);

            if (LibLoader.FilenameLocations.TryGetValue($"{name.Name}.dll", out var path))
            {
                if (File.Exists(path))
                    return AssemblyDefinition.ReadAssembly(path, parameters);
            }
            else if (LibLoader.FilenameLocations.TryGetValue($"{name.Name}.{name.Version}.dll", out path))
            {
                if (File.Exists(path))
                    return AssemblyDefinition.ReadAssembly(path, parameters);
            }


            return base.Resolve(name, parameters);
        }
    }

    internal static class LibLoader
    {
        internal static string LibraryPath => Path.Combine(Environment.CurrentDirectory, "Libs");
        internal static string NativeLibraryPath => Path.Combine(LibraryPath, "Native");
        internal static Dictionary<string, string> FilenameLocations;

        internal static void Configure()
        {
            SetupAssemblyFilenames(true);
            AppDomain.CurrentDomain.AssemblyResolve -= AssemblyLibLoader;
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;
        }

        internal static void SetupAssemblyFilenames(bool force = false)
        {
            if (FilenameLocations == null || force)
            {
                FilenameLocations = new Dictionary<string, string>();

                foreach (var fn in TraverseTree(LibraryPath, s => s != NativeLibraryPath))
                    if (FilenameLocations.ContainsKey(fn.Name))
                        Log(Logger.Level.Critical, $"Multiple instances of {fn.Name} exist in Libs! Ignoring {fn.FullName}");
                    else FilenameLocations.Add(fn.Name, fn.FullName);


                if (!SetDefaultDllDirectories(LoadLibraryFlags.LOAD_LIBRARY_SEARCH_USER_DIRS | LoadLibraryFlags.LOAD_LIBRARY_SEARCH_SYSTEM32
                                            | LoadLibraryFlags.LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LoadLibraryFlags.LOAD_LIBRARY_SEARCH_APPLICATION_DIR))
                {
                    var err = new Win32Exception();
                    Log(Logger.Level.Critical, $"Error configuring DLL search path");
                    Log(Logger.Level.Critical, err);
                    return;
                }

                static void AddDir(string path)
                {
                    var retPtr = AddDllDirectory(path);
                    if (retPtr == IntPtr.Zero)
                    {
                        var err = new Win32Exception();
                        Log(Logger.Level.Warning, $"Could not add DLL directory {path}");
                        Log(Logger.Level.Warning, err);
                    }
                }

                if (Directory.Exists(NativeLibraryPath))
                {
                    AddDir(NativeLibraryPath);
                    TraverseTree(NativeLibraryPath, dir =>
                    { // this is a terrible hack for iterating directories
                        AddDir(dir); return true;
                    }).All(f => true); // force it to iterate all
                }

                //var unityData = Directory.EnumerateDirectories(Environment.CurrentDirectory, "*_Data").First();
                //AddDir(Path.Combine(unityData, "Plugins"));

                foreach (var dir in Environment.GetEnvironmentVariable("path").Split(Path.PathSeparator))
                    AddDir(dir);
            }
        }

        public static Assembly AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            var asmName = new AssemblyName(e.Name);
            return LoadLibrary(asmName);
        }

        internal static Assembly LoadLibrary(AssemblyName asmName)
        {
            Log(Logger.Level.Debug, $"Resolving library {asmName}");

            SetupAssemblyFilenames();

            var testFile = $"{asmName.Name}.dll";
            Log(Logger.Level.Debug, $"Looking for file {asmName.Name}.dll");

            if (FilenameLocations.TryGetValue(testFile, out var path))
            {
                Log(Logger.Level.Debug, $"Found file {testFile} as {path}");
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);

                Log(Logger.Level.Critical, $"but {path} no longer exists!");
            }
            else if (FilenameLocations.TryGetValue(testFile = $"{asmName.Name}.{asmName.Version}.dll", out path))
            {
                Log(Logger.Level.Debug, $"Found file {testFile} as {path}");
                Log(Logger.Level.Warning, $"File {testFile} should be renamed to just {asmName.Name}.dll");
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);

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
        internal static void Log(Logger.Level lvl, Exception message)
        { // multiple proxy methods to delay loading of assemblies until it's done
            if (Logger.LogCreated)
                AssemblyLibLoaderCallLogger(lvl, message);
            else
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                Console.WriteLine($"[{lvl}] {message}");
        }

        private static void AssemblyLibLoaderCallLogger(Logger.Level lvl, string message) => Logger.libLoader.Log(lvl, message);
        private static void AssemblyLibLoaderCallLogger(Logger.Level lvl, Exception message) => Logger.libLoader.Log(lvl, message);

        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-iterate-through-a-directory-tree
        private static IEnumerable<FileInfo> TraverseTree(string root, Func<string, bool> dirValidator = null)
        {
            if (dirValidator == null) dirValidator = s => true;

            Stack<string> dirs = new Stack<string>(32);

            if (!Directory.Exists(root))
                throw new ArgumentException();
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                }
                catch (UnauthorizedAccessException)
                { continue; }
                catch (DirectoryNotFoundException)
                { continue; }

                string[] files;
                try
                {
                    files = Directory.GetFiles(currentDir);
                }
                catch (UnauthorizedAccessException)
                { continue; }
                catch (DirectoryNotFoundException)
                { continue; }
                
                foreach (string str in subDirs)
                    if (dirValidator(str)) dirs.Push(str);

                foreach (string file in files)
                {
                    FileInfo nextValue;
                    try
                    {
                        nextValue = new FileInfo(file);
                    }
                    catch (FileNotFoundException)
                    { continue; }

                    yield return nextValue;
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AddDllDirectory(string lpPathName);

        [Flags]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private enum LoadLibraryFlags : uint
        {
            None = 0,
            LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
            LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,
            LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
            LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(LoadLibraryFlags dwFlags);
    }
}
