using IPA.Loader;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BSIPA_ModList.UI
{
    internal struct WarningEntry
    {
        public string ModName;
        public string[] MissingDependencies;
        public string[] DisabledDependencies;

        public WarningEntry(string modName, string[] missingDependencies, string[] disabledDependencies)
        {
            ModName = modName;
            MissingDependencies = missingDependencies;
            DisabledDependencies = disabledDependencies;
        }
    }

    internal class WarningUI : MonoBehaviour
    {
        internal static WarningUI Instance;
        internal static bool firstShow = true;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
        }

        private void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            if (to.name == "EmptyTransition")
            {
                if (Instance != null)
                {
                    Instance.StopAllCoroutines();
                    Destroy(Instance.gameObject);
                    _mainFlow = null;
                }
                Instance = null;
            }
        }

        public void Init()
        {
            Instance = this;

            Logger.log.Debug("Warning UI Awake");
            if (firstShow)
            {
                firstShow = false;
                StartCoroutine(LookForUnmetDependencies());
            }
        }

        private static MainFlowCoordinator _mainFlow;
        private static SimpleDialogPromptViewController _warningDialog;
        private static Queue<WarningEntry> _warningsQueue = new Queue<WarningEntry>();

        private static IEnumerator LookForUnmetDependencies()
        {
            Logger.log.Debug("Waiting for MainFlowCoordinator to appear...");

            yield return new WaitWhile(() => FindObjectOfType<MainFlowCoordinator>() == null);

            Logger.log.Debug("Looking for unmet dependencies...");

            lock (Instance)
            {
                if (_mainFlow == null)
                {
                    _mainFlow = FindObjectOfType<MainFlowCoordinator>();
                    _warningDialog = _mainFlow.GetPrivateField<SimpleDialogPromptViewController>("_simpleDialogPromptViewController");
                }

                _warningsQueue.Clear();
                Dictionary<string, SemVer.Version> loadedPlugins = PluginManager.AllPlugins.Select(x => x.Metadata).Concat(PluginManager.DisabledPlugins).Concat(PluginLoader.ignoredPlugins).ToDictionary(x => x.Id, y => y.Version);

                foreach (var meta in PluginManager.AllPlugins.Select(x => x.Metadata).Concat(PluginManager.DisabledPlugins).Concat(PluginLoader.ignoredPlugins))
                {
                    List<string> disabledDependencies = new List<string>();
                    List<string> missingDependencies = new List<string>();
                    foreach (var dep in meta.Manifest.Dependencies)
                    {
#if DEBUG
                        Logger.log.Debug($"Looking for dependency {dep.Key} with version range {dep.Value.Intersect(new SemVer.Range("*.*.*"))}");
#endif

                        if (loadedPlugins.ContainsKey(dep.Key) && dep.Value.IsSatisfied(loadedPlugins[dep.Key]))
                        {
                            Logger.log.Debug($"Dependency {dep.Key} was found, but disabled.");
                            disabledDependencies.Add($"{dep.Key}@{dep.Value.ToString()}");
                        }
                        else
                        {
                            Logger.log.Debug($"{meta.Name} is missing dependency {dep.Key}@{dep.Value}");
                            missingDependencies.Add($"{dep.Key}@{dep.Value.ToString()}");
                        }
                    }

                    if(disabledDependencies.Count > 0 || missingDependencies.Count > 0)
                    {
                        _warningsQueue.Enqueue(new WarningEntry(meta.Name, missingDependencies.ToArray(), disabledDependencies.ToArray()));
                    }
                }

                if (_warningsQueue.Count > 0)
                {
                    yield return new WaitWhile(() => !_mainFlow.isActivated);

                    ShowWarningDialog();
                }
                
                yield break;
            }
        }

        private static void ShowWarningDialog()
        {
            WarningEntry warning = _warningsQueue.Dequeue();
            _warningDialog.Init("Unmet Dependencies", $"Mod <b>{warning.ModName}</b> has unmet dependencies!" +
                                                        (warning.MissingDependencies.Length > 0 ? $"\nMissing:\n<color=red>{string.Join("\n", warning.MissingDependencies)}</color>" : "") +
                                                        (warning.DisabledDependencies.Length > 0 ? $"\nDisabled:\n<color=#C2C2C2>{string.Join("\n", warning.DisabledDependencies)}</color>" : "")
                                                        , "Okay", WarningDialogDidFinish);
            _mainFlow.InvokePrivateMethod("PresentViewController", _warningDialog, null, true);
        }

        private static void WarningDialogDidFinish(int button)
        {
            _mainFlow.InvokePrivateMethod("DismissViewController", _warningDialog, null, (_warningsQueue.Count > 0));

            if (_warningsQueue.Count > 0)
            {
                ShowWarningDialog();
            }
        }
    }
}
