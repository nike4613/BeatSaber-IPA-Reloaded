using IPA.Utilities;
using IPA.Loader;
using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Logger = IPA.Logging.Logger;
using IPA.Updating.Backup;

namespace IPA.Updating.ModsaberML
{
    class Updater : MonoBehaviour
    {
        public static Updater instance;

        public void Awake()
        {
            try
            {
                if (instance != null)
                    Destroy(this);
                else
                {
                    instance = this;
                    CheckForUpdates();
                }
            }
            catch (Exception e)
            {
                Logger.log.Error(e);
            }
        }

        public void CheckForUpdates()
        {
            StartCoroutine(CheckForUpdatesCoroutine());
        }

        private struct UpdateStruct
        {
            public PluginManager.BSPluginMeta plugin;
            public ApiEndpoint.Mod externInfo;
        }
            
        IEnumerator CheckForUpdatesCoroutine()
        {
            Logger.log.Info("Checking for mod updates...");

            var toUpdate = new List<UpdateStruct>();
            var GameVersion = new Version(Application.version);

            foreach (var plugin in PluginManager.BSMetas)
            {
                var info = plugin.ModsaberInfo;
                if (info == null) continue;

                using (var request = UnityWebRequest.Get(ApiEndpoint.ApiBase + string.Format(ApiEndpoint.GetApprovedEndpoint, info.InternalName)))
                {
                    yield return request.SendWebRequest();

                    if (request.isNetworkError)
                    {
                        Logger.log.Error("Network error while trying to update mods");
                        Logger.log.Error(request.error);
                        continue;
                    }
                    if (request.isHttpError)
                    {
                        if (request.responseCode == 404)
                        {
                            Logger.log.Error($"Mod {plugin.Plugin.Name} not found under name {info.InternalName}");
                            continue;
                        }

                        Logger.log.Error($"Server returned an error code while trying to update mod {plugin.Plugin.Name}");
                        Logger.log.Error(request.error);
                        continue;
                    }

                    var json = request.downloadHandler.text;

                    ApiEndpoint.Mod modRegistry;
                    try
                    {
                        modRegistry = JsonConvert.DeserializeObject<ApiEndpoint.Mod>(json);
                        Logger.log.Debug(modRegistry.ToString());
                    }
                    catch (Exception e)
                    {
                        Logger.log.Error($"Parse error while trying to update mods");
                        Logger.log.Error(e);
                        continue;
                    }

                    Logger.log.Debug($"Found Modsaber.ML registration for {plugin.Plugin.Name} ({info.InternalName})");
                    Logger.log.Debug($"Installed version: {info.CurrentVersion}; Latest version: {modRegistry.Version}");
                    if (modRegistry.Version > info.CurrentVersion)
                    {
                        Logger.log.Debug($"{plugin.Plugin.Name} needs an update!");
                        if (modRegistry.GameVersion == GameVersion)
                        {
                            Logger.log.Debug($"Queueing update...");
                            toUpdate.Add(new UpdateStruct
                            {
                                plugin = plugin,
                                externInfo = modRegistry
                            });
                        }
                        else
                        {
                            Logger.log.Warn($"Update avaliable for {plugin.Plugin.Name}, but for a different Beat Saber version!");
                        }
                    }
                }
            }

            Logger.log.Info($"{toUpdate.Count} mods need updating");

            if (toUpdate.Count == 0) yield break;

            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            foreach (var item in toUpdate)
            {
                StartCoroutine(UpdateModCoroutine(item, tempDirectory));
            }
        }

        class StreamDownloadHandler : DownloadHandlerScript
        {
            public MemoryStream Stream { get; set; }

            public StreamDownloadHandler(MemoryStream stream) : base()
            {
                Stream = stream;
            }

            protected override void ReceiveContentLength(int contentLength)
            {
                Stream.Capacity = contentLength;
                Logger.log.Debug($"Got content length: {contentLength}");
            }

            protected override void CompleteContent()
            {
                Logger.log.Debug("Download complete");
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || data.Length < 1)
                {
                    Logger.log.Debug("CustomWebRequest :: ReceiveData - received a null/empty buffer");
                    return false;
                }

                Stream.Write(data, 0, dataLength);
                return true;
            }

            protected override byte[] GetData() { return null; }

            protected override float GetProgress()
            {
                return 0f;
            }

            public override string ToString()
            {
                return $"{base.ToString()} ({Stream?.ToString()})";
            }
        }

        private void ExtractPluginAsync(MemoryStream stream, UpdateStruct item, ApiEndpoint.Mod.PlatformFile fileInfo, string tempDirectory)
        {
            Logger.log.Debug($"Extracting ZIP file for {item.plugin.Plugin.Name}");

            var data = stream.GetBuffer();
            SHA1 sha = new SHA1CryptoServiceProvider();
            var hash = sha.ComputeHash(data);
            if (!LoneFunctions.UnsafeCompare(hash, fileInfo.Hash))
                throw new Exception("The hash for the file doesn't match what is defined");

            var newFiles = new List<FileInfo>();
            var backup = new BackupUnit(tempDirectory, $"backup-{item.plugin.ModsaberInfo.InternalName}");

            try
            {
                bool shouldDeleteOldFile = true;

                using (var zipFile = ZipFile.Read(stream))
                {
                    Logger.log.Debug("Streams opened");
                    foreach (var entry in zipFile)
                    {
                        if (entry.IsDirectory)
                        {
                            Logger.log.Debug($"Creating directory {entry.FileName}");
                            Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, entry.FileName));
                        }
                        else
                        {
                            using (var ostream = new MemoryStream((int)entry.UncompressedSize))
                            {
                                entry.Extract(ostream);
                                ostream.Seek(0, SeekOrigin.Begin);

                                sha = new SHA1CryptoServiceProvider();
                                var fileHash = sha.ComputeHash(ostream);
                                if (!LoneFunctions.UnsafeCompare(fileHash, fileInfo.FileHashes[entry.FileName]))
                                    throw new Exception("The hash for the file doesn't match what is defined");

                                ostream.Seek(0, SeekOrigin.Begin);
                                FileInfo targetFile = new FileInfo(Path.Combine(Environment.CurrentDirectory, entry.FileName));
                                Directory.CreateDirectory(targetFile.DirectoryName);

                                if (targetFile.FullName == item.plugin.Filename)
                                    shouldDeleteOldFile = false; // overwriting old file, no need to delete

                                if (targetFile.Exists)
                                    backup.Add(targetFile);
                                else
                                    newFiles.Add(targetFile);

                                Logger.log.Debug($"Extracting file {targetFile.FullName}");

                                var fstream = targetFile.Create();
                                ostream.CopyTo(fstream);
                            }
                        }
                    }
                }

                if (item.plugin.Plugin is SelfPlugin)
                { // currently updating self
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.plugin.Filename,
                        Arguments = $"--waitfor={Process.GetCurrentProcess().Id} --nowait",
                        UseShellExecute = false
                    });
                }
                else if (shouldDeleteOldFile)
                    File.Delete(item.plugin.Filename);
            }
            catch (Exception)
            { // something failed; restore
                foreach (var file in newFiles)
                    file.Delete();
                backup.Restore();
                backup.Delete();

                throw;
            }

            backup.Delete();

            Logger.log.Debug("Downloader exited");
        }

        IEnumerator UpdateModCoroutine(UpdateStruct item, string tempDirectory)
        {
            Logger.log.Debug($"Steam avaliable: {SteamCheck.IsAvailable}");

            ApiEndpoint.Mod.PlatformFile platformFile;
            if (SteamCheck.IsAvailable || item.externInfo.Files.Oculus == null)
                platformFile = item.externInfo.Files.Steam;
            else
                platformFile = item.externInfo.Files.Oculus;

            string url = platformFile.DownloadPath;

            Logger.log.Debug($"URL = {url}");

            const int MaxTries = 3;
            int maxTries = MaxTries;
            while (maxTries > 0)
            {
                if (maxTries-- != MaxTries)
                    Logger.log.Info($"Re-trying download...");

                using (var stream = new MemoryStream())
                using (var request = UnityWebRequest.Get(url))
                using (var taskTokenSource = new CancellationTokenSource())
                {
                    var dlh = new StreamDownloadHandler(stream);
                    request.downloadHandler = dlh;

                    Logger.log.Debug("Sending request");
                    //Logger.log.Debug(request?.downloadHandler?.ToString() ?? "DLH==NULL");
                    yield return request.SendWebRequest();
                    Logger.log.Debug("Download finished");

                    if (request.isNetworkError)
                    {
                        Logger.log.Error("Network error while trying to update mod");
                        Logger.log.Error(request.error);
                        taskTokenSource.Cancel();
                        continue;
                    }
                    if (request.isHttpError)
                    {
                        Logger.log.Error($"Server returned an error code while trying to update mod");
                        Logger.log.Error(request.error);
                        taskTokenSource.Cancel();
                        continue;
                    }

                    stream.Seek(0, SeekOrigin.Begin); // reset to beginning

                    var downloadTask = Task.Run(() =>
                    { // use slightly more multithreaded approach than coroutines
                        ExtractPluginAsync(stream, item, platformFile, tempDirectory);
                    }, taskTokenSource.Token);

                    while (!(downloadTask.IsCompleted || downloadTask.IsCanceled || downloadTask.IsFaulted))
                        yield return null; // pause coroutine until task is done

                    if (downloadTask.IsFaulted)
                    {
                        Logger.log.Error($"Error downloading mod {item.plugin.Plugin.Name}");
                        Logger.log.Error(downloadTask.Exception);
                        continue;
                    }

                    break;
                }
            }

            if (maxTries == 0)
                Logger.log.Warn($"Plugin download failed {MaxTries} times, not re-trying");
            else
                Logger.log.Debug("Download complete");
        }
    }
}
