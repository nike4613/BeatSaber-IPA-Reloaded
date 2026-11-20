#nullable enable
using IPA.AntiMalware;
using IPA.Config;
using IPA.Logging;
using IPA.Utilities;
using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace IPA.Loader
{
    internal class CecilLibLoader : DefaultAssemblyResolver
    {
        private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        private static readonly string CurrentAssemblyPath = Assembly.GetExecutingAssembly().Location;

        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            parameters.AssemblyResolver ??= this;

            if (name.Name == CurrentAssemblyName)
                return AssemblyDefinition.ReadAssembly(CurrentAssemblyPath, parameters);

            if (LibLoader.FilenameLocations.TryGetValue($"{name.Name}.dll", out var assemblyPath) ||
                LibLoader.FilenameLocations.TryGetValue($"{name.Name}.{name.Version}.dll", out assemblyPath))
            {
                if (File.Exists(assemblyPath))
                    return AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
            }

            return base.Resolve(name, parameters);
        }
    }

    internal static class LibLoader
    {
        private static readonly string LibraryPath = Path.Combine(Environment.CurrentDirectory, "Libs");
        private static readonly string NativeLibraryPath = Path.Combine(LibraryPath, "Native");
        internal static readonly Dictionary<string, string> FilenameLocations = [];

        internal static void Configure()
        {
            SetupAssemblyFilenames();
            Environment.SetEnvironmentVariable("Path", NativeLibraryPath + Path.PathSeparator + Environment.GetEnvironmentVariable("Path"));
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;
        }

        private static void SetupAssemblyFilenames()
        {
            var files = Directory.EnumerateFiles(DoorstopEnvironment.ManagedFolderDir)
                .Concat(Directory.EnumerateFiles(LibraryPath, "*", SearchOption.TopDirectoryOnly))
                .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                if (!FilenameLocations.TryGetValue(fileName, out var storedPath))
                {
                    FilenameLocations[fileName] = file;
                    continue;
                }

                var ignoredPath = file;

                if (GetFileVersion(file) > GetFileVersion(storedPath))
                {
                    FilenameLocations[fileName] = file;
                    ignoredPath = storedPath;
                }

                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Notice, $"Multiple instances of {fileName} exist! Ignoring {ignoredPath}");
            }

            static Version GetFileVersion(string path)
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion;
                return fileVersion != null && Version.TryParse(fileVersion, out var version) ? version : new Version(0, 0, 0, 0);
            }
        }

        private static Assembly? AssemblyLibLoader(object source, ResolveEventArgs e)
        {
            var asmName = new AssemblyName(e.Name);

            Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Resolving library {asmName}");

            var testFile = $"{asmName.Name}.dll";
            Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Looking for file {asmName.Name}.dll");

            if (FilenameLocations.TryGetValue(testFile, out var assemblyPath))
            {
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Found file {testFile} as {assemblyPath}");
                return LoadSafe(assemblyPath);
            }
            if (FilenameLocations.TryGetValue(testFile = $"{asmName.Name}.{asmName.Version}.dll", out assemblyPath))
            {
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Debug, $"Found file {testFile} as {assemblyPath}");
                Logger.Log(Logger.LoggerId.LibLoader, Logger.Level.Warning, $"File {testFile} should be renamed to just {asmName.Name}.dll");
                return LoadSafe(assemblyPath);
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

        // https://github.com/mono/mono/blob/0f53e9e151d92944cacab3e24ac359410c606df6/mono/metadata/image.c#L2035-L2047
        // https://github.com/mono/mono/blob/0f53e9e151d92944cacab3e24ac359410c606df6/mono/metadata/assembly.c#L2996-L3029
        internal static void LoadFromMemory(AssemblyDefinition assemblyDefinition, string path)
        {
            var monoHandle = DynDll.OpenLibrary(DoorstopEnvironment.MonoLibPath);
            var monoImageOpenFromDataWithName = Marshal.GetDelegateForFunctionPointer<MonoImageOpenFromDataWithNameDelegate>(monoHandle.GetExport("mono_image_open_from_data_with_name"));
            var monoAssemblyLoadFromFull = Marshal.GetDelegateForFunctionPointer<MonoAssemblyLoadFromFullDelegate>(monoHandle.GetExport("mono_assembly_load_from_full"));

            using var memoryStream = new MemoryStream();
            assemblyDefinition.Write(memoryStream);

            var status = MonoImageOpenStatus.Ok;
            var image = monoImageOpenFromDataWithName(memoryStream.GetBuffer(), (uint)memoryStream.Length, true, ref status, false, path);

            if (image == IntPtr.Zero)
            {
                throw new BadImageFormatException($"Failed to load image {path}: {status}");
            }

            if (monoAssemblyLoadFromFull(image, path, ref status, false) == IntPtr.Zero)
            {
                throw new BadImageFormatException($"Failed to load assembly {path}: {status}");
            }
        }

        private enum MonoImageOpenStatus
        {
            Ok,
            ErrorErrNo,
            MissingAssemblyRef,
            ImageInvalid
        }

        private delegate IntPtr MonoImageOpenFromDataWithNameDelegate(byte[] data, uint dataLength, bool needCopy,
            ref MonoImageOpenStatus status, bool refOnly, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        private delegate IntPtr MonoAssemblyLoadFromFullDelegate(IntPtr image,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string fileName, ref MonoImageOpenStatus status, bool refOnly);
    }
}
