using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using CommonMark;
using CommonMark.Syntax;
using UnityEngine.UI;
using TMPro;
using CustomUI.BeatSaber;

namespace BSIPA_ModList.UI.ViewControllers
{
    [RequireComponent(typeof(RectTransform), typeof(ScrollRect))]
    public class MarkdownView : MonoBehaviour
    {
        private class TagTypeComponent : MonoBehaviour
        {
            internal BlockTag Tag;
            internal HeadingData hData;
        }

        private string markdown = "";
        public string Markdown
        {
            get => markdown;
            set
            {
                markdown = value;
                UpdateMd();
            }
        }

        public RectTransform rectTransform => GetComponent<RectTransform>();

        private ScrollRect view;
        private RectTransform content;
        private RectTransform viewport;
        private Scrollbar scrollbar;

        private CommonMarkSettings settings;
        public MarkdownView()
        {
            settings = CommonMarkSettings.Default.Clone();
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.All;
            settings.RenderSoftLineBreaksAsLineBreaks = false;
            settings.UriResolver = ResolveUri;
        }

        public Func<string, bool> HasEmbeddedImage;

        private string ResolveUri(string arg)
        {
            var name = arg.Substring(3);
            if (!arg.StartsWith("!::") && !arg.StartsWith("w::"))
            { // !:: means embedded, w:: means web
              // this block is for when neither is specified

                Logger.md.Debug($"Resolving nonspecific URI {arg}");
                // check if its embedded
                if (HasEmbeddedImage != null && HasEmbeddedImage(arg))
                    return "!::" + arg;
                else
                    return "w::" + arg;
            }

            Logger.md.Debug($"Resolved specific URI {arg}");
            return arg;
        }

        protected void Awake()
        {
            rectTransform.sizeDelta = new Vector2(100f, 100f);
            view = GetComponent<ScrollRect>();
            view.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            view.vertical = true;
            view.horizontal = false;
            view.scrollSensitivity = 0f;
            view.movementType = ScrollRect.MovementType.Clamped;

            scrollbar = new GameObject("Scrollbar", typeof(RectTransform)).AddComponent<Scrollbar>();
            scrollbar.transform.SetParent(transform);
            scrollbar.direction = Scrollbar.Direction.TopToBottom;
            scrollbar.interactable = true;
            view.verticalScrollbar = scrollbar;

            var vpgo = new GameObject("Viewport");
            viewport = vpgo.AddComponent<RectTransform>();
            viewport.SetParent(transform);
            viewport.localPosition = Vector2.zero;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            vpgo.AddComponent<Mask>();

            view.viewport = viewport;

            content = new GameObject("Content Wrapper").AddComponent<RectTransform>();
            content.SetParent(viewport);
            content.localPosition = Vector2.zero;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            var contentLayout = content.gameObject.AddComponent<LayoutElement>();
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.MinSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentLayout.preferredWidth = contentLayout.minWidth = 100f; // to be adjusted
            contentLayout.preferredHeight = 0f;
            content.gameObject.AddComponent<TagTypeComponent>();

            view.content = content;
        }

        private static Sprite whitePixel;
        private static Sprite WhitePixel
        {
            get
            {
                if (whitePixel == null)
                    whitePixel = Resources.FindObjectsOfTypeAll<Sprite>().First(s => s.name == "WhitePixel");
                return whitePixel;
            }
        }

#if DEBUG
#if UI_CONFIGURE_MARKDOWN_THEMATIC_BREAK
        private byte tbreakSettings = 0;
#endif
        public void Update()
        {
#if UI_CONFIGURE_MARKDOWN_THEMATIC_BREAK
            if (Input.GetKeyDown(KeyCode.K))
            {
                tbreakSettings = (byte)((tbreakSettings + 1) % 16);
                UpdateMd();
                Logger.md.Info(tbreakSettings.ToString());
            }
#endif
        }
#endif

        private void UpdateMd()
        {
            Clear();

            var doc = CommonMarkConverter.Parse(markdown, settings);

            Stack<RectTransform> layout = new Stack<RectTransform>();
            layout.Push(content);
            TextMeshProUGUI currentText = null;
            foreach (var node in doc.AsEnumerable())
            {
                Logger.md.Debug($"node {node}");

                if (node.Block != null)
                {
                    var block = node.Block;

                    HorizontalOrVerticalLayoutGroup BlockNode(string name, float spacing, bool isVertical, Action<TagTypeComponent> apply = null, bool isDoc = false)
                    {
                        var type = isVertical ? typeof(VerticalLayoutGroup) : typeof(HorizontalLayoutGroup);
                        if (node.IsOpening)
                        {
                            Logger.md.Debug($"Creating block container {name}");

                            currentText = null;
                            var go = new GameObject(name, typeof(RectTransform), type);
                            var vlayout = go.GetComponent<RectTransform>();
                            vlayout.SetParent(layout.Peek());
                            vlayout.anchoredPosition = Vector2.zero;
                            vlayout.localScale = Vector3.one;
                            vlayout.localPosition = Vector3.zero;
                            var tt = go.AddComponent<TagTypeComponent>();
                            tt.Tag = block.Tag;
                            apply?.Invoke(tt);
                            layout.Push(vlayout);

                            HorizontalOrVerticalLayoutGroup l;
                            if (isVertical)
                                l = go.GetComponent<VerticalLayoutGroup>();
                            else
                                l = go.GetComponent<HorizontalLayoutGroup>();

                            l.childControlHeight = l.childControlWidth = true;
                            l.childForceExpandHeight = l.childForceExpandWidth = false;
                            l.childForceExpandWidth = isDoc;
                            l.spacing = spacing;
                            return l;
                        }
                        else if (node.IsClosing)
                        {
                            currentText = null;
                            layout.Pop();
                        }
                        return null;
                    }

                    switch (block.Tag)
                    {
                        case BlockTag.Document:
                            BlockNode("DocumentRoot", .2f, true, isDoc: true);
                            break;
                        case BlockTag.SetextHeading:
                            var l = BlockNode("SeHeading", .1f, false, t => t.hData = block.Heading);
                            if (l) l.childAlignment = TextAnchor.UpperCenter; // TODO: fix centering
                            break;
                        case BlockTag.AtxHeading:
                                l = BlockNode("AtxHeading", .1f, false, t => t.hData = block.Heading);
                            if (l && block.Heading.Level == 1)
                                l.childAlignment = TextAnchor.UpperCenter;
                            break;
                        case BlockTag.Paragraph:
                            BlockNode("Paragraph", .1f, false);
                            break;
                        case BlockTag.ThematicBreak:
                            { // TODO: fix this, it doesn't want to actually show up properly
                                const float BreakHeight = .5f;

                                var go = new GameObject("ThematicBreak", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                                var vlayout = go.GetComponent<RectTransform>();
                                vlayout.SetParent(layout.Peek());
                                vlayout.anchoredPosition = Vector2.zero;

                                l = go.GetComponent<HorizontalLayoutGroup>();
#if DEBUG && UI_CONFIGURE_MARKDOWN_THEMATIC_BREAK
                                l.childControlHeight = (tbreakSettings & 0b0001) != 0; // if set, not well behaved
                                l.childControlWidth = (tbreakSettings & 0b0010) != 0;
                                l.childForceExpandHeight = (tbreakSettings & 0b0100) != 0; // if set, not well behaved
                                l.childForceExpandWidth = (tbreakSettings & 0b1000) != 0;
#else
                                l.childControlHeight = false;
                                l.childControlWidth = false;
                                l.childForceExpandHeight = false;
                                l.childForceExpandWidth = false;
#endif
                                l.spacing = 0f;

                                currentText = null;
                                go = new GameObject("ThematicBreakBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                                var im = go.GetComponent<Image>();
                                im.color = Color.white;
                                im.sprite = WhitePixel;
                                im.material = new Material(CustomUI.Utilities.UIUtilities.NoGlowMaterial);
                                var rt = go.GetComponent<RectTransform>();
                                rt.SetParent(vlayout);
                                rt.anchorMin = Vector2.zero;
                                rt.anchorMax = Vector2.one;
                                rt.sizeDelta = new Vector2(100f, BreakHeight);
                                var le = go.GetComponent<LayoutElement>();
                                le.minWidth = le.preferredWidth = 100f;
                                le.minHeight = le.preferredHeight = BreakHeight;
                                le.flexibleHeight = le.flexibleWidth = 1f;
                            }
                            break;
                        // TODO: add the rest of the tag types
                    }
                }
                else if (node.Inline != null)
                { // inline element
                    var inl = node.Inline;

                    const float PSize = 3.5f;
                    const float H1Size = 4.8f;
                    const float HLevelDecrease = 0.5f;
                    switch (inl.Tag)
                    {
                        case InlineTag.String:
                            if (currentText == null)
                            {
                                Logger.md.Debug($"Adding new text element");

                                var tt = layout.Peek().gameObject.GetComponent<TagTypeComponent>();
                                currentText = BeatSaberUI.CreateText(layout.Peek(), "", Vector2.zero);
                                //var le = currentText.gameObject.AddComponent<LayoutElement>();
                                
                                switch (tt.Tag)
                                {
                                    case BlockTag.List:
                                    case BlockTag.ListItem:
                                    case BlockTag.Paragraph:
                                        currentText.fontSize = PSize;
                                        currentText.enableWordWrapping = true;
                                        break;
                                    case BlockTag.AtxHeading:
                                        var size = H1Size;
                                        size -= HLevelDecrease * (tt.hData.Level - 1);
                                        currentText.fontSize = size;
                                        currentText.enableWordWrapping = true;
                                        break;
                                    case BlockTag.SetextHeading:
                                        currentText.fontSize = H1Size;
                                        currentText.enableWordWrapping = true;
                                        break;
                                    // TODO: add other relevant types
                                }
                            }
                            Logger.md.Debug($"Appending '{inl.LiteralContent}' to current element");
                            currentText.text += inl.LiteralContent;
                            break;
                    }
                }
            }
        }

        private void Clear()
        {
            content.gameObject.SetActive(false);
            void Clear(Transform target)
            {
                foreach (Transform child in target)
                {
                    Clear(child);
                    Logger.md.Debug($"Destroying {child.name}");
                    Destroy(child.gameObject);
                }
            }
            Clear(content);
            content.gameObject.SetActive(true);
        }
    }
}
