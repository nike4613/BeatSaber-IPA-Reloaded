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
        public string[] IgnoredDependencies;
        public string[] DisabledDependencies;

        public WarningEntry(string modName, string[] missingDependencies, string[] ignoredDependencies, string[] disabledDependencies)
        {
            ModName = modName;
            MissingDependencies = missingDependencies;
            IgnoredDependencies = ignoredDependencies;
            DisabledDependencies = disabledDependencies;
        }
    }

    internal class WarningUI : MonoBehaviour
    { // TODO: rework this to just use disable/ignore reason
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
                    _warningDialog = _mainFlow.GetField<SimpleDialogPromptViewController>("_simpleDialogPromptViewController");
                }

                _warningsQueue.Clear();

                var enabledPlugins = PluginManager.AllPlugins.Select(p => p.Metadata).NonNull(x => x.Id).ToDictionary(x => x.Id, y => y.Version);
                var ignoredPlugins = PluginLoader.ignoredPlugins.NonNull(x => x.Id).ToDictionary(x => x.Id, y => y.Version);
                var disabledPlugins = PluginManager.DisabledPlugins.NonNull(x => x.Id).ToDictionary(x => x.Id, y => y.Version);

                // iterate only disabled and ignored, as thats where missing deps can end up
                foreach (var meta in PluginManager.DisabledPlugins.Concat(PluginLoader.ignoredPlugins))
                {
                    List<string> disabledDependencies = new List<string>();
                    List<string> ignoredDependencies = new List<string>();
                    List<string> missingDependencies = new List<string>();
                    foreach (var dep in meta.Manifest.Dependencies)
                    {
#if DEBUG
                        Logger.log.Debug($"Looking for dependency {dep.Key} with version range {dep.Value.Intersect(new SemVer.Range("*.*.*"))}");
#endif

                        if (disabledPlugins.TryGetValue(dep.Key, out var version) && dep.Value.IsSatisfied(version))
                        {
                            Logger.log.Debug($"Dependency {dep.Key} was found, but disabled.");
                            disabledDependencies.Add($"{dep.Key} {dep.Value.ToString()}");
                        }
                        else if (ignoredPlugins.TryGetValue(dep.Key, out version) && dep.Value.IsSatisfied(version))
                        {
                            Logger.log.Debug($"Dependency {dep.Key} was found, but was ignored, likely due to a missing dependency.");
                            ignoredDependencies.Add($"{dep.Key} {dep.Value.ToString()}");
                        }
                        else if (enabledPlugins.TryGetValue(dep.Key, out version) && dep.Value.IsSatisfied(version))
                        {
                            // do nothing, this was probably user disabled
                        }
                        else
                        {
                            Logger.log.Debug($"{meta.Name} is missing dependency {dep.Key} {dep.Value}");
                            missingDependencies.Add($"{dep.Key} {dep.Value.ToString()}");
                        }

                    }

                    if(disabledDependencies.Count > 0 || ignoredDependencies.Count > 0 || missingDependencies.Count > 0)
                        _warningsQueue.Enqueue(new WarningEntry(meta.Name, missingDependencies.ToArray(), ignoredDependencies.ToArray(), disabledDependencies.ToArray()));
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
                                                        (warning.IgnoredDependencies.Length > 0 ? $"\nIgnored:\n<color=#C2B2B2>{string.Join("\n", warning.IgnoredDependencies)}</color>" : "") +
                                                        (warning.DisabledDependencies.Length > 0 ? $"\nDisabled:\n<color=#C2C2C2>{string.Join("\n", warning.DisabledDependencies)}</color>" : "")
                                                        , "Okay", WarningDialogDidFinish);
            _mainFlow.InvokeMethod("PresentViewController", _warningDialog, null, true);
        }

        private static void WarningDialogDidFinish(int button)
        {
            _mainFlow.InvokeMethod("DismissViewController", _warningDialog, null, (_warningsQueue.Count > 0));

            if (_warningsQueue.Count > 0)
            {
                ShowWarningDialog();
            }
        }
    }
}
