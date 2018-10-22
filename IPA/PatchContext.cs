using System;
using System.IO;
using System.Reflection;

namespace IPA
{
    public class PatchContext
    {
        /// <summary>
        /// Gets the filename of the executable.
        /// </summary>
        public string Executable { get; private set; }

        public string DataPathSrc { get; private set; }
        public string LibsPathSrc { get; private set; }
        public string PluginsFolder { get; private set; }
        public string ProjectName { get; private set; }
        public string DataPathDst { get; private set; }
        public string LibsPathDst { get; private set; }
        public string ManagedPath { get; private set; }
        public string EngineFile { get; private set; }
        public string AssemblyFile { get; private set; }
        public string ProjectRoot { get; private set; }
        public string IPARoot { get; private set; }
        public string ShortcutPath { get; private set; }
        public string IPA { get; private set; }
        public string BackupPath { get; private set; }

        private PatchContext() { }

        public static PatchContext Create(string exe)
        {
            var context = new PatchContext
            {
                Executable = exe
            };
            context.ProjectRoot = new FileInfo(context.Executable).Directory?.FullName;
            context.IPARoot = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? throw new InvalidOperationException(), "IPA");
            context.IPA = Assembly.GetExecutingAssembly().Location;
            context.DataPathSrc = Path.Combine(context.IPARoot, "Data");
            context.LibsPathSrc = Path.Combine(context.IPARoot, "Libs");
            context.PluginsFolder = Path.Combine(context.ProjectRoot ?? throw new InvalidOperationException(), "Plugins");
            context.ProjectName = Path.GetFileNameWithoutExtension(context.Executable);
            context.DataPathDst = Path.Combine(context.ProjectRoot, context.ProjectName + "_Data");
            context.LibsPathDst = Path.Combine(context.ProjectRoot, "Libs");
            context.ManagedPath = Path.Combine(context.DataPathDst, "Managed");
            context.EngineFile = Path.Combine(context.ManagedPath, "UnityEngine.CoreModule.dll");
            context.AssemblyFile = Path.Combine(context.ManagedPath, "Assembly-CSharp.dll");
            context.BackupPath = Path.Combine(context.IPARoot, "Backups", context.ProjectName);
            string shortcutName = $"{context.ProjectName} (Patch & Launch)";
            context.ShortcutPath = Path.Combine(context.ProjectRoot, shortcutName) + ".lnk";

            Directory.CreateDirectory(context.BackupPath);

            return context;
        }
    }
}
