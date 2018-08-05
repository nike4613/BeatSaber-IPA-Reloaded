using IllusionInjector.Utilities;
using Ionic.Zip;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Logger = IllusionInjector.Logging.Logger;

namespace IllusionInjector.Updating.ModsaberML
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

            var modList = new List<ApiEndpoint.Mod>();
            using (var request = UnityWebRequest.Get(ApiEndpoint.ApiBase+ApiEndpoint.GetApprovedEndpoint))
            {
                yield return request.SendWebRequest();

                if (request.isNetworkError)
                {
                    Logger.log.Error("Network error while trying to update mods");
                    Logger.log.Error(request.error);
                    yield break;
                }
                if (request.isHttpError)
                {
                    Logger.log.Error($"Server returned an error code while trying to update mods");
                    Logger.log.Error(request.error);
                }

                var json = request.downloadHandler.text;

                JSONObject obj = null;
                try
                {
                    obj = JSON.Parse(json).AsObject;
                }
                catch (InvalidCastException)
                {
                    Logger.log.Error($"Parse error while trying to update mods");
                    Logger.log.Error($"Response doesn't seem to be a JSON object");
                    yield break;
                }
                catch (Exception e)
                {
                    Logger.log.Error($"Parse error while trying to update mods");
                    Logger.log.Error(e);
                    yield break;
                }

                foreach (var modObj in obj["mods"].AsArray.Children)
                {
                    try
                    {
                        modList.Add(ApiEndpoint.Mod.DecodeJSON(modObj.AsObject));
                    }
                    catch (Exception e)
                    {
                        Logger.log.Error($"Parse error while trying to update mods");
                        Logger.log.Error($"Response doesn't seem to be correctly formatted");
                        Logger.log.Error(e);
                        break;
                    }
                }
            }

            var GameVersion = new Version(Application.version);

            foreach (var plugin in PluginManager.BSMetas)
            {
                var info = plugin.ModsaberInfo;
                var modRegistry = modList.FirstOrDefault(o => o.Name == info.InternalName);
                if (modRegistry != null)
                { // a.k.a we found it 
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
            Logger.log.Debug($"Created temp download dirtectory {tempDirectory}");
            foreach (var item in toUpdate)
            {
                StartCoroutine(UpdateModCoroutine(tempDirectory, item));
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
                Logger.log.Debug("ReceiveData");
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

        private void ExtractPluginAsync(MemoryStream stream, UpdateStruct item, ApiEndpoint.Mod.PlatformFile fileInfo)
        {
            Logger.log.Debug($"Getting ZIP file for {item.plugin.Plugin.Name}");
            //var stream = await httpClient.GetStreamAsync(url);

            var data = stream.GetBuffer();
            SHA1 sha = new SHA1CryptoServiceProvider();
            var hash = sha.ComputeHash(data);
            if (!LoneFunctions.UnsafeCompare(hash, fileInfo.Hash))
                throw new Exception("The hash for the file doesn't match what is defined");

            using (var zipFile = ZipFile.Read(stream))
            {
                Logger.log.Debug("Streams opened");
                foreach (var entry in zipFile)
                {
                    Logger.log.Debug(entry?.FileName ?? "NULL");

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
                            if (targetFile.Exists)
                            {
                                Logger.log.Debug($"Target file {targetFile.FullName} exists");
                            }

                            var fstream = targetFile.Create();
                            ostream.CopyTo(fstream);

                            Logger.log.Debug($"Wrote file {targetFile.FullName}");
                        }
                    }
                }
            }

            Logger.log.Debug("Downloader exited");
        }

        IEnumerator UpdateModCoroutine(string tempdir, UpdateStruct item)
        {
            ApiEndpoint.Mod.PlatformFile platformFile;
            if (SteamCheck.IsAvailable || item.externInfo.OculusFile == null)
                platformFile = item.externInfo.SteamFile;
            else
                platformFile = item.externInfo.OculusFile;

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
                        ExtractPluginAsync(stream, item, platformFile);
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
