using CustomUI.BeatSaber;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BSIPA_ModList.UI
{
    internal class ButtonUI : MonoBehaviour
    {
        private const string ControllerPanel = "MainMenuViewController/SmallButtons";
        private const string CopyButton = "CreditsButton";

        internal static ButtonUI Instance;

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
                    menuFlow = null;
                }
                Instance = null;
            }
        }

        public void Init()
        {
            Instance = this;

            Logger.log.Debug("UI Awake");
            StartCoroutine(AddModListButton());
        }

        private static ModListFlowCoordinator menuFlow;

        private static readonly WaitUntil _bottomPanelExists = new WaitUntil(() => GameObject.Find(ControllerPanel) != null);
        private static RectTransform panel;

        private static HoverHint hintText;
        private static Button button;

        private static IEnumerator AddModListButton()
        {
            yield return _bottomPanelExists;

            Logger.log.Debug("Adding button to main menu");

            lock (Instance)
            {
                if (menuFlow == null)
                    menuFlow = new GameObject("BSIPA Mod List Flow Controller").AddComponent<ModListFlowCoordinator>();
                if (panel == null)
                    panel = GameObject.Find(ControllerPanel).transform as RectTransform;

                if (button == null)
                {
                    button = BeatSaberUI.CreateUIButton(panel, CopyButton, () =>
                    {
                        menuFlow.Present();
                    }, "Mod List");
                    panel.Find(CopyButton).SetAsLastSibling();

                    hintText = BeatSaberUI.AddHintText(button.transform as RectTransform, "View and control updates for installed mods");
                }

                yield break;
            }
        }
    }
}
