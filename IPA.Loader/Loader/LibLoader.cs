#nullable enable
using IPA.AntiMalware;
using IPA.Config;
using IPA.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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

            if (LibLoader.FilenameLocations.TryGetValue($"{name.Name}.dll", out var assemblyInfo))
            {
                if (File.Exists(assemblyInfo.Path))
                    return AssemblyDefinition.ReadAssembly(assemblyInfo.Path, parameters);
            }
            else if (LibLoader.FilenameLocations.TryGetValue($"{name.Name}.{name.Version}.dll", out assemblyInfo))
            {
                if (File.Exists(assemblyInfo.Path))
                    return AssemblyDefinition.ReadAssembly(assemblyInfo.Path, parameters);
            }


            return base.Resolve(name, parameters);
        }
    }

    internal static class LibLoader
    {
        internal static string LibraryPath => Path.Combine(Environment.CurrentDirectory, "Libs");
        internal static string NativeLibraryPath => Path.Combine(LibraryPath, "Native");
        internal static Dictionary<string, (string Path, Version Version)> FilenameLocations = null!;

        internal static void Configure()
        {
            SetupAssemblyFilenames(true);
            AppDomain.CurrentDomain.AssemblyResolve -= AssemblyLibLoader;
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;
        }

        private static readonly object lockObj = new();

        [ThreadStatic]
        private static bool thisThreadIsInitializing;

        internal static void SetupAssemblyFilenames(bool force = false)
        {
            if (FilenameLocations == null || force)
            {
                lock (lockObj)
                {
                    if (thisThreadIsInitializing)
                    {
                        Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Warning, "Library loader initialization recursion detected");
                        return;
                    }
                    if (FilenameLocations is not null && !force) return;

                    thisThreadIsInitializing = true;

                    var newFilenameLocations = new Dictionary<string, (string, Version Version)>();

                    var files = TraverseTree(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!)
                        .Concat(TraverseTree(LibraryPath, s => s != NativeLibraryPath));

                    foreach (var fileInfo in files)
                    {
                        if (!fileInfo.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var versionInfo = FileVersionInfo.GetVersionInfo(fileInfo.FullName).FileVersion;
                        if (versionInfo == null || !Version.TryParse(versionInfo, out var fileVersion))
                        {
                            fileVersion = new Version(0, 0, 0, 0);
                        }

                        if (!newFilenameLocations.TryGetValue(fileInfo.Name, out var assemblyInfo) || fileVersion > assemblyInfo.Version)
                        {
                            newFilenameLocations[fileInfo.Name] = (fileInfo.FullName, fileVersion);
                        }
                        else
                        {
                            Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Notice, $"Multiple instances of {fileInfo.Name} exist! Ignoring {fileInfo.FullName}");
                        }
                    }

                    static void AddDirectoryToPath(string path)
                    {
                        Environment.SetEnvironmentVariable("Path", path + Path.PathSeparator + Environment.GetEnvironmentVariable("Path"));
                    }

                    if (Directory.Exists(NativeLibraryPath))
                    {
                        AddDirectoryToPath(NativeLibraryPath);
                        _ = TraverseTree(NativeLibraryPath, dir =>
                        { // this is a terrible hack for iterating directories
                            AddDirectoryToPath(dir); return true;
                        }).All(f => true); // force it to iterate all
                    }

                    FilenameLocations = newFilenameLocations;

                    _ = LoadLibrary(new AssemblyName("netstandard, Version=2.0.0.0, Culture=neutral"));

                    thisThreadIsInitializing = false;
                }
            }
        }

        public static Assembly? AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            var asmName = new AssemblyName(e.Name);
            return LoadLibrary(asmName);
        }

        internal static Assembly? LoadLibrary(AssemblyName asmName)
        {
            Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Resolving library {asmName}");

            SetupAssemblyFilenames();

            var testFile = $"{asmName.Name}.dll";
            Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Looking for file {asmName.Name}.dll");

            if (FilenameLocations.TryGetValue(testFile, out var assemblyInfo))
            {
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Found file {testFile} as {assemblyInfo.Path}");
                return LoadSafe(assemblyInfo.Path);
            }
            else if (FilenameLocations.TryGetValue(testFile = $"{asmName.Name}.{asmName.Version}.dll", out assemblyInfo))
            {
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Found file {testFile} as {assemblyInfo.Path}");
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Warning, $"File {testFile} should be renamed to just {asmName.Name}.dll");
                return LoadSafe(assemblyInfo.Path);
            }

            Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Critical, $"No library {asmName} found");

            return null;
        }

        private static Assembly? LoadSafe(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Critical, $"{path} no longer exists!");
                return null;
            }

            if (AntiMalwareEngine.IsInitialized)
            {
                var result = AntiMalwareEngine.Engine.ScanFile(new FileInfo(path));
                if (result is ScanResult.Detected)
                {
                    Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Error, $"Scan of '{path}' found malware; not loading");
                    return null;
                }
                if (!SelfConfig.AntiMalware_.RunPartialThreatCode_ && result is not ScanResult.KnownSafe and not ScanResult.NotDetected)
                {
                    Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Error, $"Scan of '{path}' found partial threat; not loading. To load this, enable AntiMalware.RunPartialThreatCode in the config.");
                    return null;
                }
            }

            return Assembly.LoadFrom(path);
        }

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
