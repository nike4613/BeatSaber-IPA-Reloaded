using IllusionInjector.Logging;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using IllusionPlugin;
using System.Text.RegularExpressions;
using Logger = IllusionInjector.Logging.Logger;

namespace IllusionInjector.Updating
{
#if OLD_UPDATER
    class ModUpdater : MonoBehaviour
    {
        public ModUpdater instance;

        public void Awake()
        {
            instance = this;
            CheckForUpdates();
        }

        public void CheckForUpdates()
        {
            StartCoroutine(CheckForUpdatesCoroutine());
        }

        struct UpdateCheckQueueItem
        {
            public PluginManager.BSPluginMeta Plugin;
            public Uri UpdateUri;
            public string Name;
        }

        struct UpdateQueueItem
        {
            public PluginManager.BSPluginMeta Plugin;
            public Uri DownloadUri;
            public string Name;
            public Version NewVersion;
        }

        private Regex commentRegex = new Regex(@"(?: \/\/.+)?$", RegexOptions.Compiled | RegexOptions.Multiline);
        private Dictionary<Uri, UpdateScript> cachedRequests = new Dictionary<Uri, UpdateScript>();
        IEnumerator CheckForUpdatesCoroutine()
        {
            Logger.log.Info("Checking for mod updates...");

            var toUpdate = new List<UpdateQueueItem>();
            var plugins = new Queue<UpdateCheckQueueItem>(PluginManager.BSMetas.Select(p => new UpdateCheckQueueItem { Plugin = p, UpdateUri = p.Plugin.UpdateUri, Name = p.Plugin.Name }));

            for (; plugins.Count > 0;)
            {
                var plugin = plugins.Dequeue();

                Logger.log.Debug($"Checking for updates for {plugin.Name}");

                if (plugin.UpdateUri != null)
                {
                    if (!cachedRequests.ContainsKey(plugin.UpdateUri))
                        using (var request = UnityWebRequest.Get(plugin.UpdateUri))
                        {
                            yield return request.SendWebRequest();

                            if (request.isNetworkError)
                            {
                                Logger.log.Error("Network error while trying to update mods");
                                Logger.log.Error(request.error);
                                break;
                            }
                            if (request.isHttpError)
                            {
                                Logger.log.Error($"Server returned an error code while trying to update mod {plugin.Name}");
                                Logger.log.Error(request.error);
                            }

                            var json = request.downloadHandler.text;

                            json = commentRegex.Replace(json, "");

                            JSONObject obj = null;
                            try
                            {
                                obj = JSON.Parse(json).AsObject;
                            }
                            catch (InvalidCastException)
                            {
                                Logger.log.Error($"Parse error while trying to update mod {plugin.Name}");
                                Logger.log.Error($"Response doesn't seem to be a JSON object");
                                continue;
                            }
                            catch (Exception e)
                            {
                                Logger.log.Error($"Parse error while trying to update mod {plugin.Name}");
                                Logger.log.Error(e);
                                continue;
                            }

                            UpdateScript ss;
                            try
                            {
                                ss = UpdateScript.Parse(obj);
                            }
                            catch (Exception e)
                            {
                                Logger.log.Error($"Parse error while trying to update mod {plugin.Name}");
                                Logger.log.Error($"Script at {plugin.UpdateUri} doesn't seem to be a valid update script");
                                Logger.log.Debug(e);
                                continue;
                            }

                            cachedRequests.Add(plugin.UpdateUri, ss);
                        }

                    var script = cachedRequests[plugin.UpdateUri];
                    if (script.Info.TryGetValue(plugin.Name, out UpdateScript.PluginVersionInfo info))
                    {
                        Logger.log.Debug($"Checking version info for {plugin.Name} ({plugin.Plugin.Plugin.Name})");
                        if (info.NewName != null || info.NewScript != null)
                            plugins.Enqueue(new UpdateCheckQueueItem
                            {
                                Plugin = plugin.Plugin,
                                Name = info.NewName ?? plugin.Name,
                                UpdateUri = info.NewScript ?? plugin.UpdateUri
                            });
                        else
                        {
                            Logger.log.Debug($"New version: {info.Version}, Current version: {plugin.Plugin.Plugin.Version}");
                            if (info.Version > plugin.Plugin.Plugin.Version)
                            { // we should update plugin
                                Logger.log.Debug($"Queueing update for {plugin.Name} ({plugin.Plugin.Plugin.Name})");

                                toUpdate.Add(new UpdateQueueItem
                                {
                                    Plugin = plugin.Plugin,
                                    DownloadUri = info.Download,
                                    Name = plugin.Name,
                                    NewVersion = info.Version
                                });
                            }
                        }
                    }
                    else
                    {
                        Logger.log.Error($"Script defined for plugin {plugin.Name} doesn't define information for {plugin.Name}");
                        continue;
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

        IEnumerator DownloadPluginCoroutine(string tempdir, UpdateQueueItem item)
        {
            var file = Path.Combine(tempdir, item.Name + ".dll");

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

            Logger.log.Info($"{item.Plugin.Plugin.Name} updated to {item.NewVersion}");
        }
    }
#endif
}
