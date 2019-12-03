using BSIPA_ModList.UI.ViewControllers;
using CustomUI.BeatSaber;
using CustomUI.MenuButton;
using IPA.Loader;
using IPA.Updating.BeatMods;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BSIPA_ModList.UI
{
    internal class ModInfoViewController : VRUIViewController
    {
        internal Sprite Icon;
        internal string Name;
        internal string Version;
        internal string Author;
        internal string Description;
        internal PluginLoader.PluginMetadata UpdateInfo;

        private static RectTransform rowTransformOriginal;

        private ModInfoView view;
        private RectTransform rowTransform;
        private Button linkHomeButton;
        private Button linkSourceButton;
        private Button linkDonateButton;

        private ModListFlowCoordinator flowController;
        private bool showEnableDisable = false;

        private TextMeshProUGUI restartMessage;
        private Button enableDisableButton;
        private new bool enabled = false;

        public void Init(Sprite icon, string name, string version, string author, string description, PluginLoader.PluginMetadata updateInfo, PluginManifest.LinksObject links = null, bool showEnDis = false, ModListFlowCoordinator mlfc = null)
        {
            showEnableDisable = showEnDis;
            Plugin.OnConfigChaned -= OptHideButton;

            Icon = icon;
            Name = name;
            Version = version;
            Author = author;
            Description = description;
            UpdateInfo = updateInfo;

            enabled = !PluginManager.IsDisabled(updateInfo);

            flowController = mlfc;

            if (rowTransformOriginal == null)
                rowTransformOriginal = MenuButtonUI.Instance.GetField<RectTransform>("menuButtonsOriginal");

            // i also have no clue why this is necessary
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);

            var go = new GameObject("Info View", typeof(RectTransform));
            go.SetActive(false);
            go.AddComponent<RectMask2D>();
            view = go.AddComponent<ModInfoView>();
            var rt = view.transform as RectTransform;
            rt.SetParent(transform);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0);
            view.Init(this);
            go.SetActive(true);

            if (showEnDis)
            {
                restartMessage = BeatSaberUI.CreateText(rectTransform, "A restart is required to apply", new Vector2(11f, 33.5f));
                restartMessage.fontSize = 4f;
                restartMessage.gameObject.SetActive(false);

                enableDisableButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(33, 32), new Vector2(25, 10), ToggleEnable);
                enableDisableButton.GetComponentInChildren<StartMiddleEndButtonBackgroundController>().SetMiddleSprite();
                UpdateButtonText();

                Plugin.OnConfigChaned += OptHideButton;
                OptHideButton(Plugin.config.Value);
            }

            SetupLinks(links);
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            base.DidDeactivate(deactivationType);

            if (deactivationType == DeactivationType.RemovedFromHierarchy)
                Plugin.OnConfigChaned -= OptHideButton;
        }

        protected override void OnDestroy()
        {
            Plugin.OnConfigChaned -= OptHideButton;
            base.OnDestroy();
        }

        ~ModInfoViewController()
        {
            Plugin.OnConfigChaned -= OptHideButton;
        }

        private void OptHideButton(SelfConfig cfg)
        {
            enableDisableButton?.gameObject?.SetActive(cfg.ShowEnableDisable);
        }

        private Action setAction = () => { };
        private void ToggleEnable()
        {
            var info = enabled ? PluginManager.InfoFromMetadata(UpdateInfo) : null;
            bool needsRestart;
            if (enabled)
                needsRestart = PluginManager.DisablePlugin(info);
            else
                needsRestart =  PluginManager.EnablePlugin(UpdateInfo);

            UpdateButtonText(!enabled, needsRestart);
            if (!needsRestart)
                flowController.exitActions.Add(setAction);
        }

        private void UpdateButtonText(bool? _enabled = null, bool _needsRestart = false)
        {
            enabled = _enabled ?? enabled;
            if (enabled)
                enableDisableButton.SetButtonText("Disable");
            else
                enableDisableButton.SetButtonText("Enable");
            restartMessage.gameObject.SetActive(_needsRestart);
        }

        public void Reload(string name, string version, string author, string description)
        {
            Name = name;
            Version = version;
            Author = author;
            Description = description;
            view.Refresh();
        }

        private void SetupLinks(PluginManifest.LinksObject links = null, Uri moreInfoLink = null)
        {
            bool addedLink = false;
            if (links != null || moreInfoLink != null)
            {
                Logger.log.Debug($"Adding links");

                rowTransform = Instantiate(rowTransformOriginal, rectTransform);
                rowTransform.anchorMin = new Vector2(0f, 0f);
                rowTransform.anchorMax = new Vector2(1f, .15f);
                rowTransform.anchoredPosition = new Vector2(-3.5f, 4f);
                rowTransform.sizeDelta = Vector2.zero;
                Destroy(rowTransform.GetComponent<StartMiddleEndButtonsGroup>());

                foreach (Transform child in rowTransform)
                {
                    child.name = string.Empty;
                    Destroy(child.gameObject);
                }

                if (links?.ProjectHome != null)
                {
                    linkHomeButton = BeatSaberUI.CreateUIButton(rowTransform, "CreditsButton", buttonText: "Home", anchoredPosition: Vector2.zero, sizeDelta: new Vector2(20, 10),
                        onClick: () => Process.Start(links.ProjectHome.ToString()));
                    addedLink = true;
                }
                if (links?.ProjectSource != null)
                {
                    linkSourceButton = BeatSaberUI.CreateUIButton(rowTransform, "CreditsButton", buttonText: "Source", anchoredPosition: Vector2.zero, sizeDelta: new Vector2(20, 10),
                        onClick: () => Process.Start(links.ProjectSource.ToString()));
                    addedLink = true;
                }
                if (links?.Donate != null)
                {
                    linkDonateButton = BeatSaberUI.CreateUIButton(rowTransform, "CreditsButton", buttonText: "Donate", anchoredPosition: Vector2.zero, sizeDelta: new Vector2(20, 10),
                        onClick: () => Process.Start(links.Donate.ToString()));
                    addedLink = true;
                }
                if (moreInfoLink != null)
                {
                    linkDonateButton = BeatSaberUI.CreateUIButton(rowTransform, "CreditsButton", buttonText: "More Info", anchoredPosition: Vector2.zero, sizeDelta: new Vector2(20, 10),
                        onClick: () => Process.Start(moreInfoLink.ToString()));
                    addedLink = true;
                }

                foreach (var cmp in rowTransform.GetComponentsInChildren<StartMiddleEndButtonBackgroundController>())
                    cmp.SetMiddleSprite();
            }
            if (UpdateInfo != null && !addedLink)
                StartCoroutine(GetMoreInfoLink());
        }

        private IEnumerator GetMoreInfoLink()
        {
            Logger.log.Debug($"Getting more info link");
            Ref<ApiEndpoint.Mod> mod = new Ref<ApiEndpoint.Mod>(null);
            if (UpdateInfo.Id == null) yield break;
            yield return Updater.GetModInfo(UpdateInfo.Id, UpdateInfo.Version.ToString(), mod);
            try { mod.Verify(); }
            catch (Exception e)
            {
                Logger.log.Warn($"Error getting more info link for mod {UpdateInfo.Id}");
                Logger.log.Warn(e);
                yield break;
            }
            SetupLinks(null, mod.Value.Link);
        }

#if DEBUG
        public void Update()
        {
#if ADJUST_INFO_BUTTON_UI_LINKS
            RectTransform rt = rowTransform;

            if (rt == null) return;

            var cpos = rt.anchoredPosition;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x - .1f, cpos.y);
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x + .1f, cpos.y);
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x, cpos.y + .1f);
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x, cpos.y - .1f);
            }
            else
                return;

            Logger.log.Debug($"Position now at {rt.anchoredPosition}");
#endif
        }
#endif
    }

    internal class ModInfoView : MonoBehaviour
    {
        private MarkdownView mdv;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI authorText;
        private Image icon;
        private ModInfoViewController controller;

        private const string TitleFormat = "{0} <size=60%>{1}";
        public void Init(ModInfoViewController controller)
        {
            this.controller = controller;

            var rectTransform = transform as RectTransform;
            rectTransform.sizeDelta = new Vector2(60f, 10f);

            titleText = BeatSaberUI.CreateText(rectTransform, string.Format(TitleFormat, controller.Name, controller.Version), new Vector2(11f, 27.5f));
            titleText.fontSize = 6f;
            authorText = BeatSaberUI.CreateText(rectTransform, controller.Author, new Vector2(11f, 22f));
            authorText.fontSize = 4.5f;

            var mdvgo = new GameObject("MarkDown Desc");
            mdvgo.SetActive(false);
            mdv = mdvgo.AddComponent<MarkdownView>();
            mdv.rectTransform.SetParent(rectTransform);
            mdv.rectTransform.anchorMin = new Vector2(.5f, .5f);
            mdv.rectTransform.anchorMax = new Vector2(.5f, .5f);
            mdv.rectTransform.anchoredPosition = new Vector2(-4f, -3.6f);
            mdv.rectTransform.sizeDelta = new Vector2(65f, 40f);
            mdvgo.SetActive(true);
            mdv.Markdown = controller.Description;

            icon = new GameObject("Mod Info View Icon", typeof(RectTransform)).AddComponent<Image>();
            icon.gameObject.SetActive(false);
            icon.rectTransform.SetParent(rectTransform, false);
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.44f); 
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(60f, 10f);
            icon.rectTransform.anchoredPosition = new Vector2(-27.8f, 27.3f);
            icon.sprite = controller.Icon;
            icon.preserveAspect = true;
            icon.useSpriteMesh = true;
            icon.material = CustomUI.Utilities.UIUtilities.NoGlowMaterial;
            icon.gameObject.SetActive(true);
        }

        public void Refresh()
        {
            titleText.text = string.Format(TitleFormat, controller.Name, controller.Version);
            authorText.text = controller.Author;
            mdv.Markdown = controller.Description;
            icon.sprite = controller.Icon;
        }

#if DEBUG
#if ADJUST_INFO_TEXT_UI_KEYS
        private int currentItem = 0;
#endif
        public void Update()
        {
#if ADJUST_INFO_TEXT_UI_KEYS
            RectTransform rt;
            switch (currentItem)
            {
                default:
                    currentItem = 0;
                    goto case 0; // idk why this is needed tbh
                case 0:
                    rt = titleText.rectTransform;
                    break;
                case 1:
                    rt = authorText.rectTransform;
                    break;
                case 2:
                    rt = descText.rectTransform;
                    break;

            }

            if (Input.GetKeyDown(KeyCode.N))
                currentItem++;

            var cpos = rt.anchoredPosition;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x - .1f, cpos.y);
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x + .1f, cpos.y);
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x, cpos.y + .1f);
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                rt.anchoredPosition = new Vector2(cpos.x, cpos.y - .1f);
            }
            else
                return;

            Logger.log.Debug($"Position now at {rt.anchoredPosition}");
#endif
#if ADJUST_INFO_ICON_UI_KEYS
            var rt = icon.rectTransform;
            if (Input.GetKey(KeyCode.Z))
            { // adjust anchormin
                var cpos = rt.anchorMin;
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    rt.anchorMin = new Vector2(cpos.x - .001f, cpos.y);
                }
                else if (Input.GetKey(KeyCode.RightArrow))
                {
                    rt.anchorMin = new Vector2(cpos.x + .001f, cpos.y);
                }
                else if (Input.GetKey(KeyCode.UpArrow))
                {
                    rt.anchorMin = new Vector2(cpos.x, cpos.y + .001f);
                }
                else if (Input.GetKey(KeyCode.DownArrow))
                {
                    rt.anchorMin = new Vector2(cpos.x, cpos.y - .001f);
                }
                else
                    return;

                Logger.log.Debug($"Anchor min now at {rt.anchorMin}");
            }
            else if(Input.GetKey(KeyCode.X))
            { // adjust anchorMax
                var cpos = rt.anchorMax;
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    rt.anchorMax = new Vector2(cpos.x - .001f, cpos.y);
                }
                else if (Input.GetKey(KeyCode.RightArrow))
                {
                    rt.anchorMax = new Vector2(cpos.x + .001f, cpos.y);
                }
                else if (Input.GetKey(KeyCode.UpArrow))
                {
                    rt.anchorMax = new Vector2(cpos.x, cpos.y + .001f);
                }
                else if (Input.GetKey(KeyCode.DownArrow))
                {
                    rt.anchorMax = new Vector2(cpos.x, cpos.y - .001f);
                }
                else
                    return;

                Logger.log.Debug($"Anchor max now at {rt.anchorMax}");
            }
            else
            {
                var cpos = rt.anchoredPosition;
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    rt.anchoredPosition = new Vector2(cpos.x - .1f, cpos.y);
                }
                else if (Input.GetKey(KeyCode.RightArrow))
                {
                    rt.anchoredPosition = new Vector2(cpos.x + .1f, cpos.y);
                }
                else if (Input.GetKey(KeyCode.UpArrow))
                {
                    rt.anchoredPosition = new Vector2(cpos.x, cpos.y + .1f);
                }
                else if (Input.GetKey(KeyCode.DownArrow))
                {
                    rt.anchoredPosition = new Vector2(cpos.x, cpos.y - .1f);
                }
                else
                    return;

                Logger.log.Debug($"Position now at {rt.anchoredPosition}");
            }
#endif
        }
#endif
    }
}
