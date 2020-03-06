using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using IPA.Patcher;

namespace IPA
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public static class Program
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Architecture
        {
            x86,
            x64,
            Unknown
        }

        public const string FileVersion = "3.99.99.10";

        public static Version Version => Assembly.GetEntryAssembly().GetName().Version;

        public static readonly ArgumentFlag ArgHelp = new ArgumentFlag("--help", "-h")          { DocString = "prints this message" };
        public static readonly ArgumentFlag ArgWaitFor = new ArgumentFlag("--waitfor", "-w")    { DocString = "waits for the specified PID to exit", ValueString = "PID" };
        public static readonly ArgumentFlag ArgForce = new ArgumentFlag("--force", "-f")        { DocString = "forces the operation to go through" };
        public static readonly ArgumentFlag ArgRevert = new ArgumentFlag("--revert", "-r")      { DocString = "reverts the IPA installation" };
        public static readonly ArgumentFlag ArgNoRevert = new ArgumentFlag("--no-revert", "-R") { DocString = "prevents a normal installation from first reverting" };
        public static readonly ArgumentFlag ArgNoWait = new ArgumentFlag("--nowait", "-n")      { DocString = "doesn't wait for user input after the operation" };
        public static readonly ArgumentFlag ArgStart = new ArgumentFlag("--start", "-s")        { DocString = "uses the specified arguments to start the game after the patch/unpatch", ValueString = "ARGUMENTS" };
        public static readonly ArgumentFlag ArgLaunch = new ArgumentFlag("--launch", "-l")      { DocString = "uses positional parameters as arguments to start the game after patch/unpatch" };

        [STAThread]
        public static void Main()
        {
            Arguments.CmdLine.Flags(ArgHelp, ArgWaitFor, ArgForce, ArgRevert, ArgNoWait, ArgStart, ArgLaunch, ArgNoRevert).Process();

            if (ArgHelp)
            {
                Arguments.CmdLine.PrintHelp();
                return;
            }

            try
            {
                if (ArgWaitFor.HasValue)
                { // wait for process if necessary
                    var pid = int.Parse(ArgWaitFor.Value);

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

                PatchContext context = null;
                
                Assembly AssemblyLibLoader(object source, ResolveEventArgs e)
                {
                    // ReSharper disable AccessToModifiedClosure
                    if (context == null) return null;
                    var libsDir = context.LibsPathSrc;
                    // ReSharper enable AccessToModifiedClosure

                    var asmName = new AssemblyName(e.Name);
                    var testFile = Path.Combine(libsDir, $"{asmName.Name}.{asmName.Version}.dll");

                    if (File.Exists(testFile))
                        return Assembly.LoadFile(testFile);

                    Console.WriteLine($"Could not load library {asmName}");

                    return null;
                }
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;

                var argExeName = Arguments.CmdLine.PositionalArgs.FirstOrDefault(s => s.EndsWith(".exe"));
                if (argExeName == null)
                    context = PatchContext.Create(new DirectoryInfo(Directory.GetCurrentDirectory()).GetFiles()
                            .First(o => o.Extension == ".exe" && o.FullName != Assembly.GetEntryAssembly().Location)
                            .FullName);
                else
                    context = PatchContext.Create(argExeName);

                // Sanitizing
                Validate(context);

                if (ArgRevert || Keyboard.IsKeyDown(Keys.LMenu))
                    Revert(context);
                else
                {
                    Install(context);
                    StartIfNeedBe(context);
                }
            }
            catch (Exception e)
            {
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
                Console.ReadKey();
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

        private static void Install(PatchContext context)
        {
            try
            {
                bool installFiles = true;
                // first, check currently installed version, if any
                if (File.Exists(Path.Combine(context.ProjectRoot, "winhttp.dll")))
                { // installed, so check version of installed assembly
                    string injectorPath = Path.Combine(context.ManagedPath, "IPA.Injector.dll");
                    if (File.Exists(injectorPath))
                    {
                        var verInfo = FileVersionInfo.GetVersionInfo(injectorPath);
                        var fileVersion = new Version(verInfo.FileVersion);

                        if (fileVersion > Version)
                            installFiles = false;
                    }
                }

                if (installFiles || ArgForce)
                {
                    var backup = new BackupUnit(context);

                    if (!ArgNoRevert)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Restoring old version... ");
                        if (BackupManager.HasBackup(context))
                            BackupManager.Restore(context);
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
                    Directory.CreateDirectory(context.PluginsFolder);
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
                Process.Start(context.Executable, ArgStart.Value);
            }
            else
            {
                var argList = Arguments.CmdLine.PositionalArgs.ToList();

                argList.Remove(context.Executable);

                if (ArgLaunch)
                {
                    Process.Start(context.Executable, Args(argList.ToArray()));
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
            Func<FileInfo, FileInfo, IEnumerable<FileInfo>> interceptor = null, bool recurse = true)
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
                    fi.CopyTo(targetFile.FullName, true);
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


        private static void Fail(string message)
        {
            Console.Error.WriteLine("ERROR: " + message);

            WaitForEnd();

            Environment.Exit(1);
        }

        public static string Args(params string[] args)
        {
            return string.Join(" ", args.Select(EncodeParameterArgument).ToArray());
        }

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
                    reader.BaseStream.Seek(60, SeekOrigin.Begin); // this location contains the offset for the PE header
                    var peOffset = reader.ReadUInt32();

                    reader.BaseStream.Seek(peOffset + 4, SeekOrigin.Begin);
                    var machine = reader.ReadUInt16();

                    if (machine == 0x8664) // IMAGE_FILE_MACHINE_AMD64
                        return Architecture.x64;
                    if (machine == 0x014c) // IMAGE_FILE_MACHINE_I386
                        return Architecture.x86;
                    if (machine == 0x0200) // IMAGE_FILE_MACHINE_IA64
                        return Architecture.x64;
                    return Architecture.Unknown;
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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        private static bool? isConsole;
        public static bool IsConsole
        {
            get
            {
                if (isConsole == null)
                    isConsole = GetConsoleWindow() != IntPtr.Zero;
                return isConsole.Value;
            }
        }

        internal static class Keyboard
        {
            [Flags]
            private enum KeyStates
            {
                None = 0,
                Down = 1,
                Toggled = 2
            }

            [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
            private static extern short GetKeyState(int keyCode);

            private static KeyStates KeyState(Keys key)
            {
                KeyStates state = KeyStates.None;

                short retVal = GetKeyState((int)key);

                //If the high-order bit is 1, the key is down
                //otherwise, it is up.
                if ((retVal & 0x8000) == 0x8000)
                    state |= KeyStates.Down;

                //If the low-order bit is 1, the key is toggled.
                if ((retVal & 1) == 1)
                    state |= KeyStates.Toggled;

                return state;
            }

            public static bool IsKeyDown(Keys key)
            {
                return KeyStates.Down == (KeyState(key) & KeyStates.Down);
            }

            public static bool IsKeyToggled(Keys key)
            {
                return KeyStates.Toggled == (KeyState(key) & KeyStates.Toggled);
            }
        }
    }
}