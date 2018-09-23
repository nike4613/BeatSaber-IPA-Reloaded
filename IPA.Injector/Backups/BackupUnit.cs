using IPA.Logging;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace IPA.Injector.Backups
{
    /// <summary>
    /// A unit for backup. WIP.
    /// </summary>
    public class BackupUnit
    {
        public string Name { get; private set; }
        
        private DirectoryInfo _BackupPath;
        private HashSet<string> _Files = new HashSet<string>();
        private FileInfo _ManifestFile;
        private static string _ManifestFileName = "$manifest$.txt";
        
        public BackupUnit(string dir) : this(dir, DateTime.Now.ToString("yyyy-MM-dd_h-mm-ss"))
        {
        }

        private BackupUnit(string dir, string name)
        {
            Name = name;
            _BackupPath = new DirectoryInfo(Path.Combine(dir, Name));
            _ManifestFile = new FileInfo(Path.Combine(_BackupPath.FullName, _ManifestFileName));
        }
        
        public static BackupUnit FromDirectory(DirectoryInfo directory, string dir)
        {
            var unit = new BackupUnit(dir, directory.Name);

            // Read Manifest
            if (unit._ManifestFile.Exists)
            {
                string manifest = File.ReadAllText(unit._ManifestFile.FullName);
                foreach (var line in manifest.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    unit._Files.Add(line);
            }
            else
            {
                foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
                {
                    if (file.Name == _ManifestFileName) continue;
                    var relativePath = file.FullName.Substring(directory.FullName.Length + 1);
                    unit._Files.Add(relativePath);
                }
            }

            return unit;
        }

        public void Add(string file)
        {
            Add(new FileInfo(file));
        }

        internal void Delete()
        {
            _BackupPath.Delete(true);
        }

        /// <summary>
        /// Adds a file to the list of changed files and backups it.
        /// </summary>
        /// <param name="path"></param>
        public void Add(FileInfo file)
        {
            var relativePath = LoneFunctions.GetRelativePath(file.FullName, Environment.CurrentDirectory);
            var backupPath = new FileInfo(Path.Combine(_BackupPath.FullName, relativePath));
            
            // Copy over
            backupPath.Directory.Create();
            if (file.Exists)
            {
                file.CopyTo(backupPath.FullName);
            }
            else
            {
                // Make empty file
                backupPath.Create().Close();
            }

            if (!_Files.Contains(relativePath))
            {
                if (!File.Exists(_ManifestFile.FullName))
                    _ManifestFile.Create().Close();
                var stream = _ManifestFile.AppendText();
                stream.WriteLine(relativePath);
                stream.Close();

                // Add to list
                _Files.Add(relativePath);
            }
        }

    }
}
