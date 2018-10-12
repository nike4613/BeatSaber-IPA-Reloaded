using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IPA.Logging.Logger;

namespace IPA.Injector
{
    class Updates
    {
        public const string DeleteFileName = Updating.ModsaberML.Updater._SpecialDeletionsFile;
        public static void InstallPendingUpdates()
        {
            var pendingDir = Path.Combine(BeatSaber.InstallPath, "IPA", "Pending");
            if (Directory.Exists(pendingDir))
            { // there are pending updates, install
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
}
