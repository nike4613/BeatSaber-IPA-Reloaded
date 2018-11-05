using IPA.Utilities;
using System;
using System.IO;
using static IPA.Logging.Logger;

namespace IPA.Injector
{
    internal static class Updates
    {
        private const string DeleteFileName = Updating.ModSaber.Updater.SpecialDeletionsFile;
        public static void InstallPendingUpdates()
        {
            var pendingDir = Path.Combine(BeatSaber.InstallPath, "IPA", "Pending");
            if (!Directory.Exists(pendingDir)) return; 
            
            // there are pending updates, install
            updater.Info("Installing pending updates");

            var toDelete = new string[0];
            var delFn = Path.Combine(pendingDir, DeleteFileName);
            if (File.Exists(delFn))
            {
                toDelete = File.ReadAllLines(delFn);
                File.Delete(delFn);
            }

            foreach (var file in toDelete)
            {
                try
                {
                    File.Delete(Path.Combine(BeatSaber.InstallPath, file));
                }
                catch (Exception e)
                {
                    updater.Error("While trying to install pending updates: Error deleting file marked for deletion");
                    updater.Error(e);
                }
            }

            #region Self Protection
            if (Directory.Exists(Path.Combine(pendingDir, "IPA")))
                Directory.Delete(Path.Combine(pendingDir, "IPA"), true);
            if (File.Exists(Path.Combine(pendingDir, "IPA.exe")))
            {
                File.Delete(Path.Combine(pendingDir, "IPA.exe"));
                if (File.Exists(Path.Combine(pendingDir, "Mono.Cecil.dll")))
                    File.Delete(Path.Combine(pendingDir, "Mono.Cecil.dll"));
            }

            #endregion

            try
            {
                LoneFunctions.CopyAll(new DirectoryInfo(pendingDir), new DirectoryInfo(BeatSaber.InstallPath));
            }
            catch (Exception e)
            {
                updater.Error("While trying to install pending updates: Error copying files in");
                updater.Error(e);
            }

            Directory.Delete(pendingDir, true);
        }
    }
}
