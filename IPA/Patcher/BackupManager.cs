using System;
using System.IO;
using System.Linq;

namespace IPA.Patcher
{
    public static class BackupManager
    {
        public static BackupUnit FindLatestBackup(PatchContext context)
        {
            new DirectoryInfo(context.BackupPath).Create();
            return new DirectoryInfo(context.BackupPath)
                .GetDirectories()
                .OrderByDescending(p => p.Name)
                .Select(p => BackupUnit.FromDirectory(p, context))
                .FirstOrDefault();
        }

        public static bool HasBackup(PatchContext context)
        {
            return FindLatestBackup(context) != null;
        }
        
        public static bool Restore(PatchContext context)
        {
            var backup = FindLatestBackup(context);
            if(backup != null)
            {
                backup.Restore();
                backup.Delete();
                DeleteEmptyDirs(context.ProjectRoot);
                return true;
            }
            return false;
        }

        public static void DeleteEmptyDirs(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                throw new ArgumentException(
                    "Starting directory is a null reference or an empty string",
                    nameof(dir));

            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    DeleteEmptyDirs(d);
                }

                var entries = Directory.EnumerateFileSystemEntries(dir);

                if (entries.Any()) return;
                try
                {
                    Directory.Delete(dir);
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }
            catch (UnauthorizedAccessException) { }
        }

    }
}
