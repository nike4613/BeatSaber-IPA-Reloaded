using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
//using System.Windows.Forms;
using IPA.Patcher;

namespace IPA
{
    public static class Program
    {
        public enum Architecture
        {
            x86,
            x64,
            Unknown
        }

        public const string FileVersion = "4.2.2.0";

        public static Version Version => Assembly.GetEntryAssembly()!.GetName().Version!;

        public static readonly ArgumentFlag ArgHelp = new("--help", "-h")          { DocString = "prints this message" };
        public static readonly ArgumentFlag ArgVersion = new("--version", "-v")    { DocString = "prints the version that will be installed and is currently installed" };
        public static readonly ArgumentFlag ArgWaitFor = new("--waitfor", "-w")    { DocString = "waits for the specified PID to exit", ValueString = "PID" };
        public static readonly ArgumentFlag ArgForce = new("--force", "-f")        { DocString = "forces the operation to go through" };
        public static readonly ArgumentFlag ArgRevert = new("--revert", "-r")      { DocString = "reverts the IPA installation" };
        public static readonly ArgumentFlag ArgNoRevert = new("--no-revert", "-R") { DocString = "prevents a normal installation from first reverting" };
        public static readonly ArgumentFlag ArgNoWait = new("--nowait", "-n")      { DocString = "doesn't wait for user input after the operation" };
        public static readonly ArgumentFlag ArgStart = new("--start", "-s")        { DocString = "uses the specified arguments to start the game after the patch/unpatch", ValueString = "ARGUMENTS" };
        public static readonly ArgumentFlag ArgLaunch = new("--launch", "-l")      { DocString = "uses positional parameters as arguments to start the game after patch/unpatch" };

        [STAThread]
        public static void Main()
        {
            Arguments.CmdLine.Flags(ArgHelp, ArgVersion, ArgWaitFor, ArgForce, ArgRevert, ArgNoWait, ArgStart, ArgLaunch, ArgNoRevert).Process();

            if (ArgHelp)
            {
                Arguments.CmdLine.PrintHelp();
                return;
            }

            if (ArgVersion)
            {
                Console.WriteLine($"BSIPA Installer version {Version}");
            }

            try
            {
                if (ArgWaitFor.HasValue && !ArgVersion)
                { // wait for process if necessary
                    var pid = int.Parse(ArgWaitFor.Value!);

                    try
                    { // wait for beat saber to exit (ensures we can modify the file)
                        var parent = Process.GetProcessById(pid);

                        Console.WriteLine($"Waiting for parent ({pid}) process to die...");

                        parent.WaitForExit();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                PatchContext? context = null;
                
                Assembly? AssemblyLibLoader(object? source, ResolveEventArgs e)
                {
                    // ReSharper disable AccessToModifiedClosure
                    if (context == null || e.Name == null) return null;
                    var libsDir = context.LibsPathSrc;
                    // ReSharper enable AccessToModifiedClosure

                    var asmName = new AssemblyName(e.Name);
                    var testFile = Path.Combine(libsDir, $"{asmName.Name}.dll");

                    if (File.Exists(testFile))
                        return Assembly.LoadFile(testFile);

                    Console.WriteLine($"Could not load library {asmName}");

                    return null;
                }
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;

                var argExeName = Arguments.CmdLine.PositionalArgs.FirstOrDefault(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                argExeName ??= new DirectoryInfo(Directory.GetCurrentDirectory()).GetFiles()
                            .FirstOrDefault(o => o.Extension == ".exe" && o.FullName != Assembly.GetEntryAssembly()!.Location)
                            ?.FullName;
                if (argExeName == null)
                {
                    Fail("Could not locate game executable");
                }
                else
                {
                    context = PatchContext.Create(argExeName);
                }

                if (ArgVersion)
                {
                    var installed = GetInstalledVersion(context);
                    if (installed == null)
                        Console.WriteLine("No currently installed version");
                    else
                        Console.WriteLine($"Installed version: {installed}");

                    return;
                }

                // Sanitizing
                Validate(context);

                if (ArgRevert /*|| Keyboard.IsKeyDown(Keys.LMenu)*/)
                {
                    Revert(context);
                }
                else
                {
                    Install(context);
                    StartIfNeedBe(context);
                }
            }
            catch (Exception e)
            {
                if (ArgVersion)
                {
                    Console.WriteLine("No currently installed version");
                    return;
                }
                Fail(e.Message);
            }

            WaitForEnd();
        }

        private static void WaitForEnd()
        {
            if (!ArgNoWait)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("[Press any key to continue]");
                Console.ResetColor();
                _ = Console.ReadKey();
            }
        }

        private static void Validate(PatchContext c)
        {
            if (!Directory.Exists(c.DataPathDst) || !File.Exists(c.EngineFile))
            {
                Fail("Game does not seem to be a Unity project. Could not find the libraries to patch.");
                Console.WriteLine($"DataPath: {c.DataPathDst}");
                Console.WriteLine($"EngineFile: {c.EngineFile}");
            }
        }

        private static Version? GetInstalledVersion(PatchContext context)
        {
            // first, check currently installed version, if any
            if (File.Exists(Path.Combine(context.ProjectRoot, "winhttp.dll")))
            { // installed, so check version of installed assembly
                string injectorPath = Path.Combine(context.ManagedPath, "IPA.Injector.dll");
                if (File.Exists(injectorPath))
                {
                    var verInfo = FileVersionInfo.GetVersionInfo(injectorPath);
                    var fileVersion = new Version(verInfo.FileVersion);

                    return fileVersion;
                }
            }

            return null;
        }

        private static void Install(PatchContext context)
        {
            try
            {
                bool installFiles = true;

                var fileVersion = GetInstalledVersion(context);

                if (fileVersion != null && fileVersion > Version)
                    installFiles = false;

                if (installFiles || ArgForce)
                {
                    var backup = new BackupUnit(context);

                    if (!ArgNoRevert)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Restoring old version... ");
                        if (BackupManager.HasBackup(context))
                            _ = BackupManager.Restore(context);
                    }

                    var nativePluginFolder = Path.Combine(context.DataPathDst, "Plugins");
                    bool isFlat = Directory.Exists(nativePluginFolder) &&
                                  Directory.GetFiles(nativePluginFolder).Any(f => f.EndsWith(".dll"));
                    bool force = !BackupManager.HasBackup(context) || ArgForce;
                    var architecture = DetectArchitecture(context.Executable);

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("Installing files... ");

                    CopyAll(new DirectoryInfo(context.DataPathSrc), new DirectoryInfo(context.DataPathDst), force,
                        backup);
                    CopyAll(new DirectoryInfo(context.LibsPathSrc), new DirectoryInfo(context.LibsPathDst), force,
                        backup);
                    CopyAll(new DirectoryInfo(context.IPARoot), new DirectoryInfo(context.ProjectRoot), force,
                        backup,
                        null, false);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Not copying files because newer version already installed");
                }

                #region Create Plugin Folder

                if (!Directory.Exists(context.PluginsFolder))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Creating plugins folder... ");
                    _ = Directory.CreateDirectory(context.PluginsFolder);
                    Console.ResetColor();
                }

                #endregion

            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Fail("Oops! This should not have happened.\n\n" + e);
            }
            Console.ResetColor();
        }

        private static void Revert(PatchContext context)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.Write("Restoring backup... ");
            if (BackupManager.Restore(context))
            {
                Console.WriteLine("Done!");
            }
            else
            {
                Console.WriteLine("Already vanilla or you removed your backups!");
            }


            if (File.Exists(context.ShortcutPath))
            {
                Console.WriteLine("Deleting shortcut...");
                File.Delete(context.ShortcutPath);
            }

            Console.WriteLine("");
            Console.WriteLine("--- Done reverting ---");

            Console.ResetColor();
        }

        private static void StartIfNeedBe(PatchContext context)
        {
            if (ArgStart.HasValue)
            {
                _ = Process.Start(context.Executable, ArgStart.Value);
            }
            else
            {
                var argList = Arguments.CmdLine.PositionalArgs.ToList();

                _ = argList.Remove(context.Executable);

                if (ArgLaunch)
                {
                    _ = Process.Start(context.Executable, Args(argList.ToArray()));
                }
            }
        }

        public static void ClearLine()
        {
            if (IsConsole)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                int tpos = Console.CursorTop;
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, tpos);
            }
        }

        private static IEnumerable<FileInfo> PassThroughInterceptor(FileInfo from, FileInfo to)
        {
            yield return to;
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target, bool aggressive, BackupUnit backup,
            Func<FileInfo, FileInfo, IEnumerable<FileInfo>>? interceptor = null, bool recurse = true)
        {
            if (interceptor == null)
            {
                interceptor = PassThroughInterceptor;
            }

            // Copy each file into the new directory.
            foreach (var fi in source.GetFiles())
            {
                foreach (var targetFile in interceptor(fi, new FileInfo(Path.Combine(target.FullName, fi.Name))))
                {
                    if (targetFile.Exists && targetFile.LastWriteTimeUtc >= fi.LastWriteTimeUtc && !aggressive)
                        continue;

                    Debug.Assert(targetFile.Directory != null, "targetFile.Directory != null");
                    targetFile.Directory?.Create();

                    LineBack();
                    ClearLine();
                    Console.WriteLine(@"Copying {0}", targetFile.FullName);
                    backup.Add(targetFile);
                    _ = fi.CopyTo(targetFile.FullName, true);
                }
            }

            // Copy each subdirectory using recursion.
            if (!recurse) return;
            foreach (var diSourceSubDir in source.GetDirectories())
            {
                var nextTargetSubDir = new DirectoryInfo(Path.Combine(target.FullName, diSourceSubDir.Name));
                CopyAll(diSourceSubDir, nextTargetSubDir, aggressive, backup, interceptor);
            }
        }

        [DoesNotReturn]
        private static void Fail(string message)
        {
            Console.Error.WriteLine("ERROR: " + message);

            WaitForEnd();

            // This is needed because in Framework, this is not marked DoesNotReturn
#pragma warning disable CS8763 // A method marked [DoesNotReturn] should not return.
            Environment.Exit(1);
        }
#pragma warning restore CS8763 // A method marked [DoesNotReturn] should not return.

        public static string Args(params string[] args)
            => string.Join(" ", args.Select(EncodeParameterArgument).ToArray());

        /// <summary>
        /// Encodes an argument for passing into a program
        /// </summary>
        /// <param name="original">The value_ that should be received by the program</param>
        /// <returns>The value_ which needs to be passed to the program for the original value_ 
        /// to come through</returns>
        public static string EncodeParameterArgument(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;
            string value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
            value = Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"");
            return value;
        }

        public static Architecture DetectArchitecture(string assembly)
        {
            using (var reader = new BinaryReader(File.OpenRead(assembly)))
            {
                var header = reader.ReadUInt16();
                if (header == 0x5a4d)
                {
                    _ = reader.BaseStream.Seek(60, SeekOrigin.Begin); // this location contains the offset for the PE header
                    var peOffset = reader.ReadUInt32();

                    _ = reader.BaseStream.Seek(peOffset + 4, SeekOrigin.Begin);
                    var machine = reader.ReadUInt16();

                    if (machine == 0x8664) // IMAGE_FILE_MACHINE_AMD64
                    {
                        return Architecture.x64;
                    }
                    else if (machine == 0x014c) // IMAGE_FILE_MACHINE_I386
                    {
                        return Architecture.x86;
                    }
                    else if (machine == 0x0200) // IMAGE_FILE_MACHINE_IA64
                    {
                        return Architecture.x64;
                    }
                    else
                    {
                        return Architecture.Unknown;
                    }
                }

                // Not a supported binary
                return Architecture.Unknown;
            }
        }

        public static void ResetLine()
        {
            if (IsConsole)
                Console.CursorLeft = 0;
            else
                Console.Write("\r");
        }

        public static void LineBack()
        {
            if (IsConsole)
                Console.CursorTop--;
            else
                Console.Write("\x1b[1A");
        }

        public static bool IsConsole => Environment.UserInteractive;
    }
}