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
using SemVer;
using Logger = IPA.Logging.Logger;
using Version = SemVer.Version;
using IPA.Updating.Backup;
using System.Runtime.Serialization;
using System.Reflection;
using static IPA.Loader.PluginManager;

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
                Logger.updater.Error(e);
            }
        }

        private void CheckForUpdates()
        {
            StartCoroutine(CheckForUpdatesCoroutine());
        }

        private class DependencyObject
        {
            public string Name { get; set; }
            public Version Version { get; set; } = null;
            public Version ResolvedVersion { get; set; } = null;
            public Range Requirement { get; set; } = null;
            public Range Conflicts { get; set; } = null;
            public bool Resolved { get; set; } = false;
            public bool Has { get; set; } = false;
            public HashSet<string> Consumers { get; set; } = new HashSet<string>();

            public bool MetaRequestFailed { get; set; } = false;

            public PluginInfo LocalPluginMeta { get; set; } = null;

            public override string ToString()
            {
                return $"{Name}@{Version}{(Resolved ? $" -> {ResolvedVersion}" : "")} - ({Requirement} ! {Conflicts}) {(Has ? $" Already have" : "")}";
            }
        }

        private Dictionary<string, string> requestCache = new Dictionary<string, string>();
        private IEnumerator GetModsaberEndpoint(string url, Ref<string> result)
        {
            if (requestCache.TryGetValue(url, out string value))
            {
                result.Value = value;
                yield break;
            }
            else
            {
                using (var request = UnityWebRequest.Get(ApiEndpoint.ApiBase + url))
                {
                    yield return request.SendWebRequest();

                    if (request.isNetworkError)
                    {
                        result.Error = new NetworkException($"Network error while trying to download: {request.error}");
                        yield break;
                    }
                    if (request.isHttpError)
                    {
                        if (request.responseCode == 404)
                        {
                            result.Error = new NetworkException("Not found");
                            yield break;
                        }

                        result.Error = new NetworkException($"Server returned error {request.error} while getting data");
                        yield break;
                    }

                    result.Value = request.downloadHandler.text;

                    requestCache[url] = result.Value;
                }
            }
        }

        private Dictionary<string, ApiEndpoint.Mod> modCache = new Dictionary<string, ApiEndpoint.Mod>();
        private IEnumerator GetModInfo(string name, string ver, Ref<ApiEndpoint.Mod> result)
        {
            var uri = string.Format(ApiEndpoint.GetModInfoEndpoint, Uri.EscapeUriString(name), Uri.EscapeUriString(ver));

            if (modCache.TryGetValue(uri, out ApiEndpoint.Mod value))
            {
                result.Value = value;
                yield break;
            }
            else
            {
                Ref<string> reqResult = new Ref<string>("");

                yield return GetModsaberEndpoint(uri, reqResult);

                try
                {
                    result.Value = JsonConvert.DeserializeObject<ApiEndpoint.Mod>(reqResult.Value);

                    modCache[uri] = result.Value;
                }
                catch (Exception e)
                {
                    result.Error = new Exception("Error decoding response", e);
                    yield break;
                }
            }
        }

        private Dictionary<string, List<ApiEndpoint.Mod>> modVersionsCache = new Dictionary<string, List<ApiEndpoint.Mod>>();
        private IEnumerator GetModVersionsMatching(string name, string range, Ref<List<ApiEndpoint.Mod>> result)
        {
            var uri = string.Format(ApiEndpoint.GetModsWithSemver, Uri.EscapeUriString(name), Uri.EscapeUriString(range));

            if (modVersionsCache.TryGetValue(uri, out List<ApiEndpoint.Mod> value))
            {
                result.Value = value;
                yield break;
            }
            else
            {
                Ref<string> reqResult = new Ref<string>("");

                yield return GetModsaberEndpoint(uri, reqResult);

                try
                {
                    result.Value = JsonConvert.DeserializeObject<List<ApiEndpoint.Mod>>(reqResult.Value);

                    modVersionsCache[uri] = result.Value;
                }
                catch (Exception e)
                {
                    result.Error = new Exception("Error decoding response", e);
                    yield break;
                }
            }
        }

        private IEnumerator CheckForUpdatesCoroutine()
        {
            var depList = new Ref<List<DependencyObject>>(new List<DependencyObject>());

            foreach (var plugin in BSMetas)
            { // initialize with data to resolve (1.1)
                if (plugin.ModsaberInfo != null)
                { // updatable
                    var msinfo = plugin.ModsaberInfo;
                    depList.Value.Add(new DependencyObject {
                        Name = msinfo.InternalName,
                        Version = new Version(msinfo.CurrentVersion),
                        Requirement = new Range($">={msinfo.CurrentVersion}"),
                        LocalPluginMeta = plugin
                    });
                }
            }

            foreach (var dep in depList.Value)
                Logger.updater.Debug($"Phantom Dependency: {dep.ToString()}");

            yield return DependencyResolveFirstPass(depList);
            
            foreach (var dep in depList.Value)
                Logger.updater.Debug($"Dependency: {dep.ToString()}");

            yield return DependencyResolveSecondPass(depList);

            foreach (var dep in depList.Value)
                Logger.updater.Debug($"Dependency: {dep.ToString()}");

            DependendyResolveFinalPass(depList);
        }

        private IEnumerator DependencyResolveFirstPass(Ref<List<DependencyObject>> list)
        {
            for (int i = 0; i < list.Value.Count; i++)
            { // Grab dependencies (1.2)
                var dep = list.Value[i];

                var mod = new Ref<ApiEndpoint.Mod>(null);

                #region TEMPORARY get latest // SHOULD BE GREATEST OF VERSION // not going to happen because of disagreements with ModSaber
                yield return GetModInfo(dep.Name, "", mod);
                #endregion

                try { mod.Verify(); }
                catch (Exception e)
                {
                    Logger.updater.Error($"Error getting info for {dep.Name}");
                    Logger.updater.Error(e);
                    dep.MetaRequestFailed = true;
                    continue;
                }

                list.Value.AddRange(mod.Value.Dependencies.Select(d => new DependencyObject { Name = d.Name, Requirement = d.VersionRange, Consumers = new HashSet<string>() { dep.Name } }));
                list.Value.AddRange(mod.Value.Conflicts.Select(d => new DependencyObject { Name = d.Name, Conflicts = d.VersionRange, Consumers = new HashSet<string>() { dep.Name } }));
            }

            var depNames = new HashSet<string>();
            var final = new List<DependencyObject>();

            foreach (var dep in list.Value)
            { // agregate ranges and the like (1.3)
                if (!depNames.Contains(dep.Name))
                { // should add it
                    depNames.Add(dep.Name);
                    final.Add(dep);
                }
                else
                {
                    var toMod = final.Where(d => d.Name == dep.Name).First();

                    if (dep.Requirement != null)
                    {
                        toMod.Requirement = toMod.Requirement.Intersect(dep.Requirement);
                        foreach (var consume in dep.Consumers)
                            toMod.Consumers.Add(consume);
                    }
                    else if (dep.Conflicts != null)
                    {
                        if (toMod.Conflicts == null)
                            toMod.Conflicts = dep.Conflicts;
                        else
                            toMod.Conflicts = new Range($"{toMod.Conflicts} || {dep.Conflicts}"); // there should be a better way to do this
                    }
                }
            }

            list.Value = final;
        }

        private IEnumerator DependencyResolveSecondPass(Ref<List<DependencyObject>> list)
        {
            foreach(var dep in list.Value)
            {
                dep.Has = dep.Version != null; // dep.Version is only not null if its already installed

                if (dep.MetaRequestFailed)
                {
                    Logger.updater.Warn($"{dep.Name} info request failed, not trying again");
                    continue;
                }

                var modsMatching = new Ref<List<ApiEndpoint.Mod>>(null);
                yield return GetModVersionsMatching(dep.Name, dep.Requirement.ToString(), modsMatching);
                try { modsMatching.Verify(); }
                catch (Exception e)
                {
                    Logger.updater.Error($"Error getting mod list for {dep.Name}");
                    Logger.updater.Error(e);
                    dep.MetaRequestFailed = true;
                    continue;
                }

                var ver = modsMatching.Value.Where(val => val.GameVersion == BeatSaber.GameVersion && val.Approved && !dep.Conflicts.IsSatisfied(val.Version)).Select(mod => mod.Version).Max(); // (2.1)
                if (dep.Resolved = ver != null) dep.ResolvedVersion = ver; // (2.2)
                dep.Has = dep.Version == dep.ResolvedVersion && dep.Resolved; // dep.Version is only not null if its already installed
            }
        }

        private void DependendyResolveFinalPass(Ref<List<DependencyObject>> list)
        { // also starts download of mods
            var toDl = new List<DependencyObject>();

            foreach (var dep in list.Value)
            { // figure out which ones need to be downloaded (3.1)
                if (dep.Resolved)
                {
                    Logger.updater.Debug($"Resolved: {dep.ToString()}");
                    if (!dep.Has)
                    {
                        Logger.updater.Debug($"To Download: {dep.ToString()}");
                        toDl.Add(dep);
                    }
                }
                else if (!dep.Has)
                {
                    Logger.updater.Warn($"Could not resolve dependency {dep}");
                }
            }

            Logger.updater.Debug($"To Download {string.Join(", ", toDl.Select(d => $"{d.Name}@{d.ResolvedVersion}"))}");

            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            Logger.updater.Debug($"Temp directory: {tempDirectory}");

            foreach (var item in toDl)
                StartCoroutine(UpdateModCoroutine(item, tempDirectory));
        }

        private IEnumerator UpdateModCoroutine(DependencyObject item, string tempDirectory)
        { // (3.2)
            Logger.updater.Debug($"Release: {BeatSaber.ReleaseType}");

            var mod = new Ref<ApiEndpoint.Mod>(null);
            yield return GetModInfo(item.Name, item.ResolvedVersion.ToString(), mod);
            try { mod.Verify(); }
            catch (Exception e)
            {
                Logger.updater.Error($"Error occurred while trying to get information for {item}");
                Logger.updater.Error(e);
                yield break;
            }

            ApiEndpoint.Mod.PlatformFile platformFile;
            if (BeatSaber.ReleaseType == BeatSaber.Release.Steam || mod.Value.Files.Oculus == null)
                platformFile = mod.Value.Files.Steam;
            else
                platformFile = mod.Value.Files.Oculus;

            string url = platformFile.DownloadPath;

            Logger.updater.Debug($"URL = {url}");

            const int MaxTries = 3;
            int maxTries = MaxTries;
            while (maxTries > 0)
            {
                if (maxTries-- != MaxTries)
                    Logger.updater.Debug($"Re-trying download...");

                using (var stream = new MemoryStream())
                using (var request = UnityWebRequest.Get(url))
                using (var taskTokenSource = new CancellationTokenSource())
                {
                    var dlh = new StreamDownloadHandler(stream);
                    request.downloadHandler = dlh;

                    Logger.updater.Debug("Sending request");
                    //Logger.updater.Debug(request?.downloadHandler?.ToString() ?? "DLH==NULL");
                    yield return request.SendWebRequest();
                    Logger.updater.Debug("Download finished");

                    if (request.isNetworkError)
                    {
                        Logger.updater.Error("Network error while trying to update mod");
                        Logger.updater.Error(request.error);
                        taskTokenSource.Cancel();
                        continue;
                    }
                    if (request.isHttpError)
                    {
                        Logger.updater.Error($"Server returned an error code while trying to update mod");
                        Logger.updater.Error(request.error);
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
                        if (downloadTask.Exception.InnerExceptions.Where(e => e is ModsaberInterceptException).Any())
                        { // any exception is an intercept exception
                            Logger.updater.Error($"Modsaber did not return expected data for {item.Name}");
                        }

                        Logger.updater.Error($"Error downloading mod {item.Name}");
                        Logger.updater.Error(downloadTask.Exception);
                        continue;
                    }

                    break;
                }
            }

            if (maxTries == 0)
                Logger.updater.Warn($"Plugin download failed {MaxTries} times, not re-trying");
            else
                Logger.updater.Debug("Download complete");
        }

        internal class StreamDownloadHandler : DownloadHandlerScript
        {
            public MemoryStream Stream { get; set; }

            public StreamDownloadHandler(MemoryStream stream) : base()
            {
                Stream = stream;
            }

            protected override void ReceiveContentLength(int contentLength)
            {
                Stream.Capacity = contentLength;
                Logger.updater.Debug($"Got content length: {contentLength}");
            }

            protected override void CompleteContent()
            {
                Logger.updater.Debug("Download complete");
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || data.Length < 1)
                {
                    Logger.updater.Debug("CustomWebRequest :: ReceiveData - received a null/empty buffer");
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

        private void ExtractPluginAsync(MemoryStream stream, DependencyObject item, ApiEndpoint.Mod.PlatformFile fileInfo, string tempDirectory)
        { // (3.3)
            Logger.updater.Debug($"Extracting ZIP file for {item.Name}");

            var data = stream.GetBuffer();
            SHA1 sha = new SHA1CryptoServiceProvider();
            var hash = sha.ComputeHash(data);
            if (!LoneFunctions.UnsafeCompare(hash, fileInfo.Hash))
                throw new Exception("The hash for the file doesn't match what is defined");

            var newFiles = new List<FileInfo>();
            var backup = new BackupUnit(tempDirectory, $"backup-{item.Name}");

            try
            {
                bool shouldDeleteOldFile = true;

                using (var zipFile = ZipFile.Read(stream))
                {
                    Logger.updater.Debug("Streams opened");
                    foreach (var entry in zipFile)
                    {
                        if (entry.IsDirectory)
                        {
                            Logger.updater.Debug($"Creating directory {entry.FileName}");
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

                                try
                                {
                                    if (!LoneFunctions.UnsafeCompare(fileHash, fileInfo.FileHashes[entry.FileName]))
                                        throw new Exception("The hash for the file doesn't match what is defined");
                                }
                                catch (KeyNotFoundException)
                                {
                                    throw new ModsaberInterceptException("ModSaber did not send the hashes for the zip's content!");
                                }

                                ostream.Seek(0, SeekOrigin.Begin);
                                FileInfo targetFile = new FileInfo(Path.Combine(Environment.CurrentDirectory, entry.FileName));
                                Directory.CreateDirectory(targetFile.DirectoryName);

                                if (targetFile.FullName == item.LocalPluginMeta?.Filename)
                                    shouldDeleteOldFile = false; // overwriting old file, no need to delete

                                if (targetFile.Exists)
                                    backup.Add(targetFile);
                                else
                                    newFiles.Add(targetFile);

                                Logger.updater.Debug($"Extracting file {targetFile.FullName}");

                                targetFile.Delete();
                                var fstream = targetFile.Create();
                                ostream.CopyTo(fstream);
                            }
                        }
                    }
                }

                if (item.LocalPluginMeta?.Plugin is SelfPlugin)
                { // currently updating self
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.LocalPluginMeta.Filename,
                        Arguments = $"-nw={Process.GetCurrentProcess().Id}",
                        UseShellExecute = false
                    });
                }
                else if (shouldDeleteOldFile && item.LocalPluginMeta != null)
                    File.Delete(item.LocalPluginMeta.Filename);
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

            Logger.updater.Debug("Extractor exited");
        }
    }

    [Serializable]
    internal class NetworkException : Exception
    {
        public NetworkException()
        {
        }

        public NetworkException(string message) : base(message)
        {
        }

        public NetworkException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NetworkException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
    
    [Serializable]
    internal class ModsaberInterceptException : Exception
    {
        public ModsaberInterceptException()
        {
        }

        public ModsaberInterceptException(string message) : base(message)
        {
        }

        public ModsaberInterceptException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ModsaberInterceptException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

}
