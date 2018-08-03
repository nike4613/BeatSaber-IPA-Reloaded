using IllusionInjector.Utilities;
using Ionic.Zip;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            protected void ReceiveContentLength(long contentLength)
            {
                Stream.Capacity = (int)contentLength;
                Logger.log.Debug($"Got content length: {contentLength}");
            }

            protected void OnContentComplete()
            {
                Logger.log.Debug("Download complete");
            }

            protected bool ReceiveData(byte[] data, long dataLength)
            {
                Stream.Write(data, 0, (int)dataLength);
                return true;
            }
        }

        IEnumerator UpdateModCoroutine(string tempdir, UpdateStruct item)
        {
            void DownloadPluginAsync(MemoryStream stream)
            { // embedded because i don't think unity likes it in the top level

                Logger.log.Debug($"Getting ZIP file for {item.plugin.Plugin.Name}");
                //var stream = await httpClient.GetStreamAsync(url);

                using (var zipFile = new ZipInputStream(new BlockingStream(stream)))
                {
                    Logger.log.Debug("Streams opened");
                    ZipEntry entry;
                    while ((entry = zipFile.GetNextEntry()) != null)
                    {
                        Logger.log.Debug(entry?.FileName);
                    }
                }

                Logger.log.Debug("Downloader exited");
            }

            string url;
            if (SteamCheck.IsAvailable || item.externInfo.OculusFile == null)
                url = item.externInfo.SteamFile;
            else
                url = item.externInfo.OculusFile;

            Logger.log.Debug($"URL = {url}");

            using (var req = UnityWebRequest.Get(url))
            using (var memStream = new MemoryStream())
            {
                req.downloadHandler = new StreamDownloadHandler
                {
                    Stream = memStream
                };

                var downloadTask = Task.Run(() =>
                { // use slightly more multithreaded approach than coroutines
                    DownloadPluginAsync(memStream);
                });

                Logger.log.Debug("Sending request");
                yield return req.SendWebRequest();

                if (req.isNetworkError)
                {
                    Logger.log.Error("Network error while trying to update mod");
                    Logger.log.Error(req.error);
                    yield break;
                }
                if (req.isHttpError)
                {
                    Logger.log.Error($"Server returned an error code while trying to update mod");
                    Logger.log.Error(req.error);
                }

                downloadTask.Wait(); // wait for the damn thing to finish
            }

            yield return null;
        }
    }
}
