using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zip;
using IPA.Loader;
using IPA.Utilities;
using Newtonsoft.Json;
using SemVer;
using UnityEngine;
using UnityEngine.Networking;
using static IPA.Loader.PluginManager;
using Logger = IPA.Logging.Logger;
using Version = SemVer.Version;

namespace IPA.Updating.BeatMods
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal class Updater : MonoBehaviour
    {
        public static Updater Instance;

        public void Awake()
        {
            try
            {
                if (Instance != null)
                    Destroy(this);
                else
                {
                    Instance = this;
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
            public Version Version { get; set; }
            public Version ResolvedVersion { get; set; }
            public Range Requirement { get; set; }
            public Range Conflicts { get; set; } // a range of versions that are not allowed to be downloaded
            public bool Resolved { get; set; }
            public bool Has { get; set; }
            public HashSet<string> Consumers { get; set; } = new HashSet<string>();

            public bool MetaRequestFailed { get; set; }
            
            public PluginLoader.PluginInfo LocalPluginMeta { get; set; }

            public override string ToString()
            {
                return $"{Name}@{Version}{(Resolved ? $" -> {ResolvedVersion}" : "")} - ({Requirement} ! {Conflicts}) {(Has ? " Already have" : "")}";
            }
        }

        private readonly Dictionary<string, string> requestCache = new Dictionary<string, string>();
        private IEnumerator GetBeatModsEndpoint(string url, Ref<string> result)
        {
            if (requestCache.TryGetValue(url, out string value))
            {
                result.Value = value;
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

        private readonly Dictionary<string, ApiEndpoint.Mod> modCache = new Dictionary<string, ApiEndpoint.Mod>();
        private IEnumerator GetModInfo(string modName, string ver, Ref<ApiEndpoint.Mod> result)
        {
            var uri = string.Format(ApiEndpoint.GetModInfoEndpoint, Uri.EscapeUriString(modName), Uri.EscapeUriString(ver));

            if (modCache.TryGetValue(uri, out ApiEndpoint.Mod value))
            {
                result.Value = value;
            }
            else
            {
                Ref<string> reqResult = new Ref<string>("");

                yield return GetBeatModsEndpoint(uri, reqResult);

                try
                {
                    result.Value = JsonConvert.DeserializeObject<List<ApiEndpoint.Mod>>(reqResult.Value).First();

                    modCache[uri] = result.Value;
                }
                catch (Exception e)
                {
                    result.Error = new Exception("Error decoding response", e);
                }
            }
        }

        private readonly Dictionary<string, List<ApiEndpoint.Mod>> modVersionsCache = new Dictionary<string, List<ApiEndpoint.Mod>>();
        private IEnumerator GetModVersionsMatching(string modName, Range range, Ref<List<ApiEndpoint.Mod>> result)
        {
            var uri = string.Format(ApiEndpoint.GetModsByName, Uri.EscapeUriString(modName));

            if (modVersionsCache.TryGetValue(uri, out List<ApiEndpoint.Mod> value))
            {
                result.Value = value;
            }
            else
            {
                Ref<string> reqResult = new Ref<string>("");

                yield return GetBeatModsEndpoint(uri, reqResult);

                try
                {
                    result.Value = JsonConvert.DeserializeObject<List<ApiEndpoint.Mod>>(reqResult.Value)
                        .Where(m => range.IsSatisfied(m.Version)).ToList();

                    modVersionsCache[uri] = result.Value;
                }
                catch (Exception e)
                {
                    result.Error = new Exception("Error decoding response", e);
                }
            }
        }

        private IEnumerator CheckForUpdatesCoroutine()
        {
            var depList = new Ref<List<DependencyObject>>(new List<DependencyObject>());

            foreach (var plugin in BSMetas)
            { // initialize with data to resolve (1.1)
                if (plugin.Metadata.Id != null)
                { // updatable
                    var msinfo = plugin.Metadata;
                    depList.Value.Add(new DependencyObject {
                        Name = msinfo.Id,
                        Version = msinfo.Version,
                        Requirement = new Range($">{msinfo.Version}"),
                        LocalPluginMeta = plugin
                    });
                }
            }

            foreach (var meta in PluginLoader.ignoredPlugins.Where(m => m.Id != null))
            {
                depList.Value.Add(new DependencyObject
                {
                    Name = meta.Id,
                    Version = meta.Version,
                    Requirement = new Range($">{meta.Version}"),
                    LocalPluginMeta = new PluginLoader.PluginInfo
                    {
                        Metadata = meta, Plugin = null
                    }
                });
            }

            foreach (var dep in depList.Value)
                Logger.updater.Debug($"Phantom Dependency: {dep}");

            yield return DependencyResolveFirstPass(depList);
            
            foreach (var dep in depList.Value)
                Logger.updater.Debug($"Dependency: {dep}");

            yield return DependencyResolveSecondPass(depList);

            foreach (var dep in depList.Value)
                Logger.updater.Debug($"Dependency: {dep}");

            DependendyResolveFinalPass(depList);
        }

        private IEnumerator DependencyResolveFirstPass(Ref<List<DependencyObject>> list)
        {
            for (int i = 0; i < list.Value.Count; i++)
            { // Grab dependencies (1.2)
                var dep = list.Value[i];

                var mod = new Ref<ApiEndpoint.Mod>(null);
                
                yield return GetModInfo(dep.Name, "", mod);

                try { mod.Verify(); }
                catch (Exception e)
                {
                    Logger.updater.Error($"Error getting info for {dep.Name}");
                    Logger.updater.Error(e);
                    dep.MetaRequestFailed = true;
                    continue;
                }

                list.Value.AddRange(mod.Value.Dependencies.Select(m => new DependencyObject
                {
                    Name = m.Name,
                    Requirement = new Range($"^{m.Version}"),
                    Consumers = new HashSet<string> { dep.Name }
                }));
                // currently no conflicts exist in BeatMods
                //list.Value.AddRange(mod.Value.Links.Dependencies.Select(d => new DependencyObject { Name = d.Name, Requirement = d.VersionRange, Consumers = new HashSet<string> { dep.Name } }));
                //list.Value.AddRange(mod.Value.Links.Conflicts.Select(d => new DependencyObject { Name = d.Name, Conflicts = d.VersionRange, Consumers = new HashSet<string> { dep.Name } }));
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
                    var toMod = final.First(d => d.Name == dep.Name);

                    if (dep.Requirement != null)
                    {
                        toMod.Requirement = toMod.Requirement.Intersect(dep.Requirement);
                        foreach (var consume in dep.Consumers)
                            toMod.Consumers.Add(consume);
                    }
                    else if (dep.Conflicts != null)
                    {
                        toMod.Conflicts = toMod.Conflicts == null
                            ? dep.Conflicts
                            : new Range($"{toMod.Conflicts} || {dep.Conflicts}");
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
                yield return GetModVersionsMatching(dep.Name, dep.Requirement, modsMatching);
                try { modsMatching.Verify(); }
                catch (Exception e)
                {
                    Logger.updater.Error($"Error getting mod list for {dep.Name}");
                    Logger.updater.Error(e);
                    dep.MetaRequestFailed = true;
                    continue;
                }

                var ver = modsMatching.Value
                    .Where(nullCheck => nullCheck != null) // entry is not null
                    //.Where(versionCheck => versionCheck.GameVersion.Version == BeatSaber.GameVersion) // game version matches
                    .Where(approvalCheck => approvalCheck.Status == ApiEndpoint.Mod.ApprovedStatus) // version approved
                    .Where(conflictsCheck => dep.Conflicts == null || !dep.Conflicts.IsSatisfied(conflictsCheck.Version)) // not a conflicting version
                    .Select(mod => mod.Version).Max(); // (2.1) get the max version
                // ReSharper disable once AssignmentInConditionalExpression
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
                    Logger.updater.Debug($"Resolved: {dep}");
                    if (!dep.Has)
                    {
                        Logger.updater.Debug($"To Download: {dep}");
                        toDl.Add(dep);
                    }
                }
                else if (!dep.Has)
                {
                    Logger.updater.Warn($"Could not resolve dependency {dep}");
                }
            }

            Logger.updater.Debug($"To Download {string.Join(", ", toDl.Select(d => $"{d.Name}@{d.ResolvedVersion}"))}");

            foreach (var item in toDl)
                StartCoroutine(UpdateModCoroutine(item));
        }

        private IEnumerator UpdateModCoroutine(DependencyObject item)
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

            /*
            ApiEndpoint.Mod.DownloadsObject platformFile;
            if (BeatSaber.ReleaseType == BeatSaber.Release.Steam || mod.Value.Files.Oculus == null)
                platformFile = mod.Value.Files.Steam;
            else
                platformFile = mod.Value.Files.Oculus;*/

            var releaseName = BeatSaber.ReleaseType == BeatSaber.Release.Steam 
                ? ApiEndpoint.Mod.DownloadsObject.TypeSteam : ApiEndpoint.Mod.DownloadsObject.TypeOculus;
            var platformFile = mod.Value.Downloads.First(f => f.Type == ApiEndpoint.Mod.DownloadsObject.TypeUniversal || f.Type == releaseName);

            string url = ApiEndpoint.BeatModBase + platformFile.Path;

            Logger.updater.Debug($"URL = {url}");

            const int maxTries = 3;
            int tries = maxTries;
            while (tries > 0)
            {
                if (tries-- != maxTries)
                    Logger.updater.Debug("Re-trying download...");

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
                        Logger.updater.Error("Server returned an error code while trying to update mod");
                        Logger.updater.Error(request.error);
                        taskTokenSource.Cancel();
                        continue;
                    }

                    stream.Seek(0, SeekOrigin.Begin); // reset to beginning

                    var downloadTask = Task.Run(() =>
                    { // use slightly more multi threaded approach than co-routines
                        // ReSharper disable once AccessToDisposedClosure
                        ExtractPluginAsync(stream, item, platformFile);
                    }, taskTokenSource.Token);

                    while (!(downloadTask.IsCompleted || downloadTask.IsCanceled || downloadTask.IsFaulted))
                        yield return null; // pause co-routine until task is done

                    if (downloadTask.IsFaulted)
                    {
                        if (downloadTask.Exception != null && downloadTask.Exception.InnerExceptions.Any(e => e is BeatmodsInterceptException))
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

            if (tries == 0)
                Logger.updater.Warn($"Plugin download failed {maxTries} times, not re-trying");
            else
                Logger.updater.Debug("Download complete");
        }

        internal class StreamDownloadHandler : DownloadHandlerScript
        {
            public MemoryStream Stream { get; set; }

            public StreamDownloadHandler(MemoryStream stream)
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

            protected override bool ReceiveData(byte[] rData, int dataLength)
            {
                if (rData == null || rData.Length < 1)
                {
                    Logger.updater.Debug("CustomWebRequest :: ReceiveData - received a null/empty buffer");
                    return false;
                }

                Stream.Write(rData, 0, dataLength);
                return true;
            }

            protected override byte[] GetData() { return null; }

            protected override float GetProgress()
            {
                return 0f;
            }

            public override string ToString()
            {
                return $"{base.ToString()} ({Stream})";
            }
        }

        private void ExtractPluginAsync(MemoryStream stream, DependencyObject item, ApiEndpoint.Mod.DownloadsObject fileInfo)
        { // (3.3)
            Logger.updater.Debug($"Extracting ZIP file for {item.Name}");

            /*var data = stream.GetBuffer();
            SHA1 sha = new SHA1CryptoServiceProvider();
            var hash = sha.ComputeHash(data);
            if (!Utils.UnsafeCompare(hash, fileInfo.Hash))
                throw new Exception("The hash for the file doesn't match what is defined");*/

            var targetDir = Path.Combine(BeatSaber.InstallPath, "IPA", Path.GetRandomFileName() + "_Pending");
            Directory.CreateDirectory(targetDir);

            var eventualOutput = Path.Combine(BeatSaber.InstallPath, "IPA", "Pending");
            if (!Directory.Exists(eventualOutput))
                Directory.CreateDirectory(eventualOutput);

            try
            {
                bool shouldDeleteOldFile = !(item.LocalPluginMeta?.Metadata.IsSelf).Unwrap();

                using (var zipFile = ZipFile.Read(stream))
                {
                    Logger.updater.Debug("Streams opened");
                    foreach (var entry in zipFile)
                    {
                        if (entry.IsDirectory)
                        {
                            Logger.updater.Debug($"Creating directory {entry.FileName}");
                            Directory.CreateDirectory(Path.Combine(targetDir, entry.FileName));
                        }
                        else
                        {
                            using (var ostream = new MemoryStream((int)entry.UncompressedSize))
                            {
                                entry.Extract(ostream);
                                ostream.Seek(0, SeekOrigin.Begin);

                                var md5 = new MD5CryptoServiceProvider();
                                var fileHash = md5.ComputeHash(ostream);

                                try
                                {
                                    if (!Utils.UnsafeCompare(fileHash, fileInfo.Hashes.Where(h => h.File == entry.FileName).Select(h => h.Hash).First()))
                                        throw new Exception("The hash for the file doesn't match what is defined");
                                }
                                catch (KeyNotFoundException)
                                {
                                    throw new BeatmodsInterceptException("BeatMods did not send the hashes for the zip's content!");
                                }

                                ostream.Seek(0, SeekOrigin.Begin);
                                FileInfo targetFile = new FileInfo(Path.Combine(targetDir, entry.FileName));
                                Directory.CreateDirectory(targetFile.DirectoryName ?? throw new InvalidOperationException());

                                if (item.LocalPluginMeta != null && 
                                    Utils.GetRelativePath(targetFile.FullName, targetDir) == Utils.GetRelativePath(item.LocalPluginMeta?.Metadata.File.FullName, BeatSaber.InstallPath))
                                    shouldDeleteOldFile = false; // overwriting old file, no need to delete

                                /*if (targetFile.Exists)
                                    backup.Add(targetFile);
                                else
                                    newFiles.Add(targetFile);*/

                                Logger.updater.Debug($"Extracting file {targetFile.FullName}");

                                targetFile.Delete();
                                using (var fstream = targetFile.Create())
                                    ostream.CopyTo(fstream);
                            }
                        }
                    }
                }
                
                if (shouldDeleteOldFile && item.LocalPluginMeta != null)
                    File.AppendAllLines(Path.Combine(targetDir, SpecialDeletionsFile), new[] { Utils.GetRelativePath(item.LocalPluginMeta?.Metadata.File.FullName, BeatSaber.InstallPath) });
            }
            catch (Exception)
            { // something failed; restore
                /*foreach (var file in newFiles)
                    file.Delete();
                backup.Restore();
                backup.Delete();*/
                Directory.Delete(targetDir, true); // delete extraction site

                throw;
            }

            if ((item.LocalPluginMeta?.Metadata.IsSelf).Unwrap())
            { // currently updating self, so copy to working dir and update
                Utils.CopyAll(new DirectoryInfo(targetDir), new DirectoryInfo(BeatSaber.InstallPath));
                var deleteFile = Path.Combine(BeatSaber.InstallPath, SpecialDeletionsFile);
                if (File.Exists(deleteFile)) File.Delete(deleteFile);
                Process.Start(new ProcessStartInfo
                {
                    // will never actually be null
                    FileName = item.LocalPluginMeta?.Metadata.File.FullName ?? throw new InvalidOperationException(),
                    Arguments = $"-nw={Process.GetCurrentProcess().Id}",
                    UseShellExecute = false
                });
            }
            else
                Utils.CopyAll(new DirectoryInfo(targetDir), new DirectoryInfo(eventualOutput), SpecialDeletionsFile);
            Directory.Delete(targetDir, true); // delete extraction site

            Logger.updater.Debug("Extractor exited");
        }

        internal const string SpecialDeletionsFile = "$$delete";
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
    internal class BeatmodsInterceptException : Exception
    {
        public BeatmodsInterceptException()
        {
        }

        public BeatmodsInterceptException(string message) : base(message)
        {
        }

        public BeatmodsInterceptException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BeatmodsInterceptException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

}
