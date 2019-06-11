using System.IO;
using System.Linq;

namespace IPA.Injector.Backups
{
    internal static class BackupManager
    {
        public static BackupUnit FindLatestBackup(string dir)
        {
            new DirectoryInfo(dir).Create();
            return new DirectoryInfo(dir)
                .GetDirectories()
                .OrderByDescending(p => p.Name)
                .Select(p => BackupUnit.FromDirectory(p, dir))
                .FirstOrDefault();
        }

        public static bool HasBackup(string dir)
        {
            return FindLatestBackup(dir) != null;
        }
    }
}
