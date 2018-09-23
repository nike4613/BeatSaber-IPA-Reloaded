using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IPA.Injector.Backups
{
    public class BackupManager
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
