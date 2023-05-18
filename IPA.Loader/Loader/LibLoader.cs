#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Linq;
using IPA.Logging;
using IPA.Utilities;
using Mono.Cecil;
using IPA.AntiMalware;
using IPA.Config;
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
        internal static Dictionary<string, string> FilenameLocations = null!;

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
                {
                    if (FilenameLocations.ContainsKey(fn.Name))
                        Log(Logger.Level.Critical, $"Multiple instances of {fn.Name} exist in Libs! Ignoring {fn.FullName}");
                    else FilenameLocations.Add(fn.Name, fn.FullName);
                }

                static void AddDir(string path)
                {
                    var pathEnvironmentVariable = Environment.GetEnvironmentVariable("Path");
                    Environment.SetEnvironmentVariable("Path", path + Path.PathSeparator + pathEnvironmentVariable);
                }

                if (Directory.Exists(NativeLibraryPath))
                {
                    AddDir(NativeLibraryPath);
                    _ = TraverseTree(NativeLibraryPath, dir =>
                    { // this is a terrible hack for iterating directories
                        AddDir(dir); return true;
                    }).All(f => true); // force it to iterate all
                }

                //var unityData = Directory.EnumerateDirectories(Environment.CurrentDirectory, "*_Data").First();
                //AddDir(Path.Combine(unityData, "Plugins"));

                // TODO: find a way to either safely remove Newtonsoft, or switch to a different JSON lib
                _ = LoadLibrary(new AssemblyName("Newtonsoft.Json, Version=12.0.0.0, Culture=neutral"));
            }
        }

        public static Assembly? AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            var asmName = new AssemblyName(e.Name);
            return LoadLibrary(asmName);
        }

        internal static Assembly? LoadLibrary(AssemblyName asmName)
        {
            Log(Logger.Level.Debug, $"Resolving library {asmName}");

            SetupAssemblyFilenames();

            var testFile = $"{asmName.Name}.dll";
            Log(Logger.Level.Debug, $"Looking for file {asmName.Name}.dll");

            if (FilenameLocations.TryGetValue(testFile, out var path))
            {
                Log(Logger.Level.Debug, $"Found file {testFile} as {path}");
                return LoadSafe(path);
            }
            else if (FilenameLocations.TryGetValue(testFile = $"{asmName.Name}.{asmName.Version}.dll", out path))
            {
                Log(Logger.Level.Debug, $"Found file {testFile} as {path}");
                Log(Logger.Level.Warning, $"File {testFile} should be renamed to just {asmName.Name}.dll");
                return LoadSafe(path);
            }

            Log(Logger.Level.Critical, $"No library {asmName} found");

            return null;
        }

        private static Assembly? LoadSafe(string path)
        {
            if (!File.Exists(path))
            {
                Log(Logger.Level.Critical, $"{path} no longer exists!");
                return null;
            }

            if (AntiMalwareEngine.IsInitialized)
            {
                var result = AntiMalwareEngine.Engine.ScanFile(new FileInfo(path));
                if (result is ScanResult.Detected)
                {
                    Log(Logger.Level.Error, $"Scan of '{path}' found malware; not loading");
                    return null;
                }
                if (!SelfConfig.AntiMalware_.RunPartialThreatCode_ && result is not ScanResult.KnownSafe and not ScanResult.NotDetected)
                {
                    Log(Logger.Level.Error, $"Scan of '{path}' found partial threat; not loading. To load this, enable AntiMalware.RunPartialThreatCode in the config.");
                    return null;
                }
            }

            return Assembly.LoadFrom(path);
        }

        internal static void Log(Logger.Level lvl, string message)
        { // multiple proxy methods to delay loading of assemblies until it's done
            if (Logger.LogCreated)
            {
                AssemblyLibLoaderCallLogger(lvl, message);
            }
            else
            {
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                    Console.WriteLine($"[{lvl}] {message}");
            }
        }
        internal static void Log(Logger.Level lvl, Exception message)
        { // multiple proxy methods to delay loading of assemblies until it's done
            if (Logger.LogCreated)
            {
                AssemblyLibLoaderCallLogger(lvl, message);
            }
            else
            {
                if (((byte)lvl & (byte)StandardLogger.PrintFilter) != 0)
                    Console.WriteLine($"[{lvl}] {message}");
            }
        }

        private static void AssemblyLibLoaderCallLogger(Logger.Level lvl, string message) => Logger.LibLoader.Log(lvl, message);
        private static void AssemblyLibLoaderCallLogger(Logger.Level lvl, Exception message) => Logger.LibLoader.Log(lvl, message);

        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-iterate-through-a-directory-tree
        private static IEnumerable<FileInfo> TraverseTree(string root, Func<string, bool>? dirValidator = null)
        {
            if (dirValidator == null) dirValidator = s => true;

            var dirs = new Stack<string>(32);

            if (!Directory.Exists(root))
                throw new ArgumentException("Directory does not exist", nameof(root));
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
    }
}
