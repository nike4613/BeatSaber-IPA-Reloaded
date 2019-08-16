using IPA.Config;
using IPA.Updating.BeatMods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static IPA.Updating.BeatMods.Updater;

namespace BSIPA_ModList
{
    internal class DownloadObject
    {
        public enum States
        {
            ToDownload, Downloading, Installing, Failed, Completed
        }

        public DependencyObject Mod;
        public Sprite Icon;
        public States State = States.ToDownload;
        public double Progress = 0;
    }

    internal class DownloadController : MonoBehaviour
    {
        private static DownloadController _instance;
        public static DownloadController Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Create();

                return _instance;
            }
        }

        public static DownloadController Create()
        {
            var inst = new GameObject("BSIPA Modlist Download Controller").AddComponent<DownloadController>();
            if (IPA.Config.SelfConfig.Updates_.AutoCheckUpdates_)
                inst.StartCoroutine(inst.StartUpdateCheck());
            return inst;
        }

        private IEnumerator StartUpdateCheck()
        {
            yield return null;
            CheckForUpdates();
        }

        private readonly List<DownloadObject> downloads = new List<DownloadObject>();
        private readonly Dictionary<DependencyObject, DownloadObject> lookup = new Dictionary<DependencyObject, DownloadObject>();

        internal IReadOnlyList<DownloadObject> Downloads => downloads;

        public event Action OnCheckForUpdates;
        public event Action<int> OnCheckForUpdatesComplete;
        public event Action OnDownloadStateChanged;
        public event Action OnDownloaderListChanged;

        private enum States
        {
            Start, Checking, UpdatesFound, Downloading, Done, DoneWithNoUpdates
        }

        private States _state = States.Start;
        private States State
        {
            get => _state;
            set
            {
                _state = value;
                OnDownloadStateChanged?.Invoke();
            }
        }

        public bool CanCheck => State == States.Start || IsDone;
        public bool CanDownload => State == States.UpdatesFound;
        public bool CanReset => State == States.UpdatesFound;
        public bool IsChecking => State == States.Checking;
        public bool IsDownloading => State == States.Downloading;
        public bool IsDone => State == States.Done || State == States.DoneWithNoUpdates;
        public bool HadUpdates => State == States.Done;

        public void Awake() => DontDestroyOnLoad(this);

        public void CheckForUpdates()
        {
            if (!CanCheck)
                throw new InvalidOperationException("Invalid state for CheckForUpdates to be called");

            State = States.Checking;
            OnCheckForUpdates?.Invoke();
            Updater.Instance.CheckForUpdates(UpdateCheckComplete);
        }

        public void ResetCheck(bool resetCache = false)
        {
            if (!CanReset)
                throw new InvalidOperationException("Invalid state for ResetCheck to be called");

            Clear();
            State = States.Start;

            if (resetCache)
                ResetRequestCache();
        }

        private void Clear()
        {
            downloads.Clear();
            lookup.Clear();

            OnDownloaderListChanged?.Invoke();
        }

        private void Add(DownloadObject obj)
        {
            downloads.Add(obj);
            lookup.Add(obj.Mod, obj);
        }

        private void Remove(DependencyObject obj)
        {
            downloads.Remove(lookup[obj]);
            lookup.Remove(obj);
            OnDownloaderListChanged?.Invoke();
        }

        private void UpdateCheckComplete(List<DependencyObject> found)
        {
            State = States.UpdatesFound;
            OnCheckForUpdatesComplete?.Invoke(found.Count);

            foreach (var dep in found)
                Add(new DownloadObject
                {
                    Mod = dep,
                    Icon = dep.IsLegacy ? Utilities.DefaultIPAIcon : Utilities.GetIcon(dep.LocalPluginMeta?.Metadata),
                    State = DownloadObject.States.ToDownload,
                    Progress = 0
                });

            OnDownloaderListChanged?.Invoke();

            if (downloads.Count == 0)
                OnAllDownloadsCompleted(false);
            else if (IPA.Config.SelfConfig.Updates_.AutoUpdate_)
                StartDownloads();
        }

        public void StartDownloads()
        {
            if (!CanDownload)
                throw new InvalidOperationException("Invalid state for StartDownloads to be called");

            State = States.Downloading;
            Updater.Instance.StartDownload(downloads.Select(d => d.Mod), _DownloadStart, _DownloadProgress, 
                _DownloadFailed, _DownloadFinished, _InstallFailed, _InstallFinished);
        }

        private void _DownloadStart(DependencyObject obj)
        {
            var dl = lookup[obj];
            dl.Progress = 0;
            dl.State = DownloadObject.States.Downloading;
        }

        private void _DownloadProgress(DependencyObject obj, long totalBytes, long currentBytes, double progress)
        {
            lookup[obj].Progress = progress;
        }

        private void _DownloadFailed(DependencyObject obj, string error)
        {
            lookup[obj].State = DownloadObject.States.Failed;
        }

        private void _DownloadFinished(DependencyObject obj)
        {
            lookup[obj].State = DownloadObject.States.Installing;
        }

        private void _InstallFailed(DependencyObject obj, Exception error)
        {
            lookup[obj].State = DownloadObject.States.Failed;
        }

        private void _InstallFinished(DependencyObject obj, bool didError)
        {
            if (!didError)
                lookup[obj].State = DownloadObject.States.Completed;

            StartCoroutine(RemoveModFromList(obj));
        }

        private IEnumerator RemoveModFromList(DependencyObject obj)
        {
            yield return new WaitForSeconds(1f);

            Remove(obj);

            if (downloads.Count == 0)
                OnAllDownloadsCompleted(true);
        }

        private void OnAllDownloadsCompleted(bool hadUpdates)
        {
            State = hadUpdates ? States.Done : States.DoneWithNoUpdates;
        }
    }
}
