using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Logger = IllusionInjector.Logging.Logger;

namespace IllusionInjector.Updating.ModsaberML
{
    class Updater : MonoBehaviour
    {
        public Updater instance;

        public void Awake()
        {
            instance = this;
            CheckForUpdates();
        }

        public void CheckForUpdates()
        {
            StartCoroutine(CheckForUpdatesCoroutine());
        }

        private Regex commentRegex = new Regex(@"(?: \/\/.+)?$", RegexOptions.Compiled | RegexOptions.Multiline);
        private Dictionary<Uri, UpdateScript> cachedRequests = new Dictionary<Uri, UpdateScript>();
        IEnumerator CheckForUpdatesCoroutine()
        {
            Logger.log.Info("Checking for mod updates...");

            var toUpdate = new List<PluginManager.BSPluginMeta>();

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
                            toUpdate.Add(plugin);
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
                StartCoroutine(DownloadPluginCoroutine(tempDirectory, item));
            }
        }

        IEnumerator DownloadPluginCoroutine(string tempdir, PluginManager.BSPluginMeta item)
        {

            yield return null;
            /*var file = Path.Combine(tempdir, item. + ".dll");

            using (var req = UnityWebRequest.Get(item.DownloadUri))
            {
                req.downloadHandler = new DownloadHandlerFile(file);
                yield return req.SendWebRequest();

                if (req.isNetworkError)
                {
                    Logger.log.Error($"Network error while trying to download update for {item.Plugin.Plugin.Name}");
                    Logger.log.Error(req.error);
                    yield break;
                }
                if (req.isHttpError)
                {
                    Logger.log.Error($"Server returned an error code while trying to download update for {item.Plugin.Plugin.Name}");
                    Logger.log.Error(req.error);
                    yield break;
                }
            }

            var pluginDir = Path.GetDirectoryName(item.Plugin.Filename);
            var newFile = Path.Combine(pluginDir, item.Name + ".dll");

            File.Delete(item.Plugin.Filename);
            if (File.Exists(newFile))
                File.Delete(newFile);
            File.Move(file, newFile);

            Logger.log.Info($"{item.Plugin.Plugin.Name} updated to {item.NewVersion}");*/
        }
    }
}
