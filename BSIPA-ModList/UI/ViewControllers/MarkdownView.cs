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
        private bool mdDirty = false;
        public string Markdown
        {
            get => markdown;
            set
            {
                markdown = value;
                mdDirty = true;
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
            view = GetComponent<ScrollRect>();
            view.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            view.vertical = true;
            view.horizontal = false;
            view.scrollSensitivity = 0f;
            view.inertia = true;
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
            viewport.anchoredPosition = new Vector2(.5f, .5f);
            viewport.sizeDelta = Vector2.zero;
            var vpmask = vpgo.AddComponent<Mask>();
            var vpim = vpgo.AddComponent<Image>(); // supposedly Mask needs an Image?
            vpmask.showMaskGraphic = false;
            vpim.color = Color.white;
            vpim.sprite = WhitePixel;
            vpim.material = CustomUI.Utilities.UIUtilities.NoGlowMaterial;

            view.viewport = viewport;

            content = new GameObject("Content Wrapper").AddComponent<RectTransform>();
            content.SetParent(viewport);
            var contentLayout = content.gameObject.AddComponent<LayoutElement>();
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentLayout.preferredWidth = contentLayout.minWidth = rectTransform.sizeDelta.x; // to be adjusted
            content.gameObject.AddComponent<TagTypeComponent>();
            content.localPosition = Vector2.zero;
            content.anchorMin = new Vector2(.5f, .5f);
            content.anchorMax = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
            //content.sizeDelta = Vector2.zero;

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
#endif
        public void Update()
        {
#if DEBUG && UI_CONFIGURE_MARKDOWN_THEMATIC_BREAK
            if (Input.GetKeyDown(KeyCode.K))
            {
                tbreakSettings = (byte)((tbreakSettings + 1) % 16);
                UpdateMd();
                Logger.md.Info(tbreakSettings.ToString());
            }
#endif
            if (mdDirty)
                UpdateMd();
            else if (resetContentPosition)
            {
                resetContentPosition = false;
                var v = content.anchoredPosition;
                v.y = -(content.rect.height / 2);
                content.anchoredPosition = v;
            }

        }

        private bool resetContentPosition = false;
        private void UpdateMd()
        {
            mdDirty = false;
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

                    void Spacer(float size = 1.5f)
                    {
                        var go = new GameObject("Spacer", typeof(RectTransform));
                        var vlayout = go.GetComponent<RectTransform>();
                        vlayout.SetParent(layout.Peek());
                        vlayout.anchorMin = new Vector2(.5f, .5f);
                        vlayout.anchorMax = new Vector2(.5f, .5f);
                        vlayout.localScale = Vector3.one;
                        vlayout.localPosition = Vector3.zero;

                        var l = go.AddComponent<LayoutElement>();
                        l.minHeight = l.preferredHeight  = size;
                    }

                    HorizontalOrVerticalLayoutGroup BlockNode(string name, float spacing, bool isVertical, Action<TagTypeComponent> apply = null, float? spacer = null, bool isDoc = false)
                    {
                        if (node.IsOpening)
                        {
                            Logger.md.Debug($"Creating block container {name}");

                            currentText = null;
                            var go = new GameObject(name, typeof(RectTransform));
                            var vlayout = go.GetComponent<RectTransform>();
                            vlayout.SetParent(layout.Peek());
                            //vlayout.anchoredPosition = new Vector2(.5f, .5f);
                            vlayout.anchorMin = new Vector2(.5f, .5f);
                            vlayout.anchorMax = new Vector2(.5f, .5f);
                            vlayout.localScale = Vector3.one;
                            vlayout.localPosition = Vector3.zero;

                            if (isDoc)
                            {
                                vlayout.sizeDelta = Vector2.zero;
                                vlayout.anchorMin = Vector2.zero;
                                vlayout.anchorMax = Vector2.one;
                            }
                            var tt = go.AddComponent<TagTypeComponent>();
                            tt.Tag = block.Tag;
                            apply?.Invoke(tt);
                            layout.Push(vlayout);

                            HorizontalOrVerticalLayoutGroup l;
                            if (isVertical)
                                l = go.AddComponent<VerticalLayoutGroup>();
                            else
                                l = go.AddComponent<HorizontalLayoutGroup>();

                            l.childControlHeight = l.childControlWidth = true;
                            l.childForceExpandHeight = l.childForceExpandWidth = false;
                            l.childForceExpandWidth = isDoc;
                            l.spacing = spacing;
                            /*var cfit = go.AddComponent<ContentSizeFitter>();
                            cfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                            cfit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;*/

                            return l;
                        }
                        else if (node.IsClosing)
                        {
                            currentText = null;
                            layout.Pop();

                            if (spacer.HasValue)
                                Spacer(spacer.Value);
                        }
                        return null;
                    }

                    switch (block.Tag)
                    {
                        case BlockTag.Document:
                            BlockNode("DocumentRoot", .5f, true, isDoc: true);
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
                                l = BlockNode("Paragraph", .1f, false, spacer: 1.5f);
                            break;
                        case BlockTag.ThematicBreak:
                            { // TODO: fix this, it doesn't want to actually show up properly
                                const float BreakHeight = .5f;

                                var go = new GameObject("ThematicBreak", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                                var vlayout = go.GetComponent<RectTransform>();
                                vlayout.SetParent(layout.Peek());
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

                                vlayout.localScale = Vector3.one;
                                vlayout.anchoredPosition = Vector2.zero;
                                vlayout.anchorMin = new Vector2(.5f, .5f);
                                vlayout.anchorMax = new Vector2(.5f, .5f);
                                vlayout.sizeDelta = new Vector2(layout.Peek().rect.width - BreakHeight, BreakHeight);
                                vlayout.localPosition = Vector3.zero;

                                currentText = null;
                                go = new GameObject("ThematicBreak Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                                var im = go.GetComponent<Image>();
                                im.color = Color.white;
                                // i think i need to copy the sprite because i'm using the same one for the mask
                                im.sprite = Sprite.Create(WhitePixel.texture, WhitePixel.rect, Vector2.zero);
                                im.material = CustomUI.Utilities.UIUtilities.NoGlowMaterial;
                                var rt = go.GetComponent<RectTransform>();
                                rt.SetParent(vlayout);
                                var le = go.GetComponent<LayoutElement>();
                                le.minWidth = le.preferredWidth = layout.Peek().rect.width - BreakHeight;
                                le.minHeight = le.preferredHeight = BreakHeight;
                                le.flexibleHeight = le.flexibleWidth = 1f;
                                rt.localScale = Vector3.one;
                                rt.localPosition = Vector3.zero;
                                rt.anchoredPosition = Vector3.zero;
                                rt.anchorMin = Vector2.zero;
                                rt.anchorMax = Vector2.one;
                                rt.sizeDelta = new Vector2(layout.Peek().rect.width - BreakHeight, BreakHeight);

                                Spacer(1f);
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

            resetContentPosition = true;
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
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }
            Clear(content);
            content.gameObject.SetActive(true);
        }
    }
}
