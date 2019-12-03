using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CommonMark;
using CommonMark.Syntax;
using UnityEngine.UI;
using TMPro;
using CustomUI.BeatSaber;
using IPA.Utilities;
using System.Reflection;
using UnityEngine.EventSystems;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BSIPA_ModList.UI.ViewControllers
{
    /// <summary>
    /// A UI component that renders CommonMark Markdown in-game.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MarkdownView : MonoBehaviour
    {
        private class TagTypeComponent : MonoBehaviour
        {
            internal BlockTag Tag;
            internal HeadingData hData;
            internal ListData lData;
            internal int listCount;
            internal int listIndex;
        }

        private string markdown = "";
        private bool mdDirty = false;
        
        /// <summary>
        /// The text to be rendered.
        /// </summary>
        /// <remarks>
        /// When this is assigned, the object is marked dirty. It will re-render on the next Update tick.
        /// </remarks>
        /// <value>the text to render as Markdown</value>
        public string Markdown
        {
            get => markdown;
            set
            {
                markdown = value;
                mdDirty = true;
            }
        }

        /// <summary>
        /// A convenience property to access the <see cref="RectTransform"/> on the <see cref="GameObject"/> this is on.
        /// </summary>
        /// <value>the <see cref="RectTransform"/> associated with this component</value>
        public RectTransform rectTransform => GetComponent<RectTransform>();

        private ScrollView scrView;
        private RectTransform content;
        private RectTransform viewport;

        private CommonMarkSettings settings;

        /// <summary>
        /// Creates a new <see cref="MarkdownView"/>. Should never be called directly. Instead, use <see cref="GameObject.AddComponent{T}"/>.
        /// </summary>
        public MarkdownView()
        {
            settings = CommonMarkSettings.Default.Clone();
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.All;
            settings.RenderSoftLineBreaksAsLineBreaks = false;
            settings.UriResolver = ResolveUri;
        }

        /// <summary>
        /// This function will be called whenever attempting to resolve an image URI, to ensure that the image exists in the embedded assembly.
        /// </summary>
        /// <value>a delegate for the function to call</value>
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

        private static string GetLinkUri(string uri)
        {
            if (uri[0] == '!')
            {
                Logger.md.Error($"Cannot link to embedded resource in mod description");
                return null;
            }
            else
                return uri.Substring(3);
        }

        private static Stream ConsolasAssetBundleFontStream => Assembly.GetExecutingAssembly().GetManifestResourceStream("BSIPA_ModList.Bundles.consolas.font");

        private static AssetBundleCreateRequest _bundleRequest;
        private static AssetBundle _bundle;
        private static AssetBundle Bundle
        {
            get
            {
                if (_bundle == null && _bundleRequest != null)
                    throw new InvalidOperationException("Asset bundle is being loaded asynchronously; please wait for that to complete");
                if (_bundle == null)
                    _bundle = AssetBundle.LoadFromStream(ConsolasAssetBundleFontStream);
                return _bundle;
            }
        }

        private static AssetBundleRequest _consolasRequest;
        private static TMP_FontAsset _unsetConsolas;
        private static TMP_FontAsset _consolas;
        private static TMP_FontAsset Consolas
        {
            get
            {
                if (_unsetConsolas == null && _consolasRequest != null)
                    throw new InvalidOperationException("Asset is being loaded asynchronously; please wait for that to complete");
                if (_unsetConsolas == null)
                    _unsetConsolas = Bundle?.LoadAsset<TMP_FontAsset>("CONSOLAS");
                if (_consolas == null && _unsetConsolas != null)
                    _consolas = SetupFont(_unsetConsolas);
                return _consolas;
            }
        }

        private static TMP_FontAsset SetupFont(TMP_FontAsset f)
        {
            var originalFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Last(f2 => f2.name == "Teko-Medium SDF No Glow");
            var matCopy = Instantiate(originalFont.material);
            matCopy.mainTexture = f.material.mainTexture;
            matCopy.mainTextureOffset = f.material.mainTextureOffset;
            matCopy.mainTextureScale = f.material.mainTextureScale;
            f.material = matCopy;
            f = Instantiate(f);
            MaterialReferenceManager.AddFontAsset(f);
            return f;
        }

        internal static void StartLoadResourcesAsync()
        {
            SharedCoroutineStarter.instance.StartCoroutine(LoadResourcesAsync());
        }
        private static IEnumerator LoadResourcesAsync()
        {
            Logger.md.Debug("Starting to load resources");

            _bundleRequest = AssetBundle.LoadFromStreamAsync(ConsolasAssetBundleFontStream);
            yield return _bundleRequest;
            _bundle = _bundleRequest.assetBundle;

            Logger.md.Debug("Bundle loaded");

            _consolasRequest = _bundle.LoadAssetAsync<TMP_FontAsset>("CONSOLAS");
            yield return _consolasRequest;
            _unsetConsolas = _consolasRequest.asset as TMP_FontAsset;

            Logger.md.Debug("Font loaded");
        }

        internal void Awake()
        {
            if (Consolas == null)
                Logger.md.Error($"Loading of Consolas font failed");

            gameObject.SetActive(false);

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

            content = new GameObject("Content Wrapper").AddComponent<RectTransform>();
            content.SetParent(viewport);
            content.gameObject.AddComponent<TagTypeComponent>();
            content.localPosition = Vector2.zero;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.anchoredPosition = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.spacing = 0f;

            var pageUp = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last((Button x) => x.name == "PageUpButton"), rectTransform, false);
            var pageDown = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last((Button x) => x.name == "PageDownButton"), rectTransform, false);

            {
                var pup_rt = pageUp.transform as RectTransform;
                var pup_sof = pup_rt.sizeDelta.y;
                var pup_xoff = (rectTransform.sizeDelta.x / 2) + (pup_sof / 2);
                pup_rt.anchoredPosition = new Vector2(pup_xoff, pup_rt.anchoredPosition.y);
                var pup_bg_rt = pup_rt.Find("BG") as RectTransform;
                pup_bg_rt.sizeDelta = new Vector2(pup_bg_rt.sizeDelta.y, pup_bg_rt.sizeDelta.y);

                // fix hitbox
                pup_rt.anchorMin = new Vector2(.5f, pup_rt.anchorMin.y);
                pup_rt.anchorMax = new Vector2(.5f, pup_rt.anchorMax.y);
                pup_rt.sizeDelta = new Vector2(pup_rt.sizeDelta.y, pup_rt.sizeDelta.y);
            }
            {
                var pdn_rt = pageDown.transform as RectTransform;
                var pdn_sof = pdn_rt.sizeDelta.y;
                var pdn_xoff = (rectTransform.sizeDelta.x / 2) + (pdn_sof / 2);
                pdn_rt.anchoredPosition = new Vector2(pdn_xoff, pdn_rt.anchoredPosition.y);
                var pdn_bg_rt = pdn_rt.Find("BG") as RectTransform;
                pdn_bg_rt.sizeDelta = new Vector2(pdn_bg_rt.sizeDelta.y, pdn_bg_rt.sizeDelta.y);

                // fix hitbox
                pdn_rt.anchorMin = new Vector2(.5f, pdn_rt.anchorMin.y);
                pdn_rt.anchorMax = new Vector2(.5f, pdn_rt.anchorMax.y);
                pdn_rt.sizeDelta = new Vector2(pdn_rt.sizeDelta.y, pdn_rt.sizeDelta.y);
            }

            scrView = gameObject.AddComponent<ScrollView>();
            scrView.SetField("_pageUpButton", pageUp);
            scrView.SetField("_pageDownButton", pageDown);
            scrView.SetField("_contentRectTransform", content);
            scrView.SetField("_viewport", viewport);

            gameObject.SetActive(true);
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

        private static Sprite blockQuoteBackground;
        private static Sprite BlockQuoteBackground
        {
            get
            {
                if (blockQuoteBackground == null)
                    blockQuoteBackground = Resources.FindObjectsOfTypeAll<Sprite>().First(s => s.name == "RoundRectNormal");
                return blockQuoteBackground;
            }
        }

        private static readonly Color BlockQuoteColor = new Color( 30f / 255, 109f / 255, 178f / 255, .25f);
        private static readonly Color BlockCodeColor  = new Color(135f / 255, 135f / 255, 135f / 255, .5f);

#if DEBUG
#if UI_CONFIGURE_MARKDOWN_THEMATIC_BREAK
        private byte tbreakSettings = 0;
#endif
#endif
        internal void Update()
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
                StartCoroutine(UpdateMd());
        }

        [Flags]
        private enum CurrentTextFlags
        {
            None = 0, Bold = 1, Italic = 2, Underline = 4, Strikethrough = 8,
        }

        private const string LinkDefaultColor = "#59B0F4";
        // private const string LinkHoverColor = "#009dff";

        private static readonly Regex linkRefTitleColorRegex = new Regex(@"\{\s*color:\s*#?([a-fA-F0-9]{6})\s*\}", RegexOptions.Compiled | RegexOptions.Singleline);

        const float PSize = 3.5f;
        const float BlockCodeSize = PSize - .5f;
        const float H1Size = 5.5f;
        const float HLevelDecrease = 0.5f;

        private IEnumerator UpdateMd()
        {
            mdDirty = false;
            Clear();

            // enable so it will set stuff up
            content.gameObject.GetComponent<VerticalLayoutGroup>().enabled = true;

            var doc = CommonMarkConverter.Parse(markdown, settings);

            Stack<RectTransform> layout = new Stack<RectTransform>();
            layout.Push(content);
            TextMeshProUGUI currentText = null;
            List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();
            CurrentTextFlags textFlags = 0;
            foreach (var node in doc.AsEnumerable())
            {
                Logger.md.Debug($"node {node}");

                if (node.Block != null)
                {
                    var block = node.Block;

                    const float BreakHeight = .5f;
                    const int TextInset = 1;
                    const int BlockQuoteInset = TextInset * 2;
                    const int BlockCodeInset = BlockQuoteInset;
                    const int ListInset = TextInset;

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

                    HorizontalOrVerticalLayoutGroup BlockNode(string name, float spacing, bool isVertical, Action<TagTypeComponent> apply = null, float? spacer = null, bool isDoc = false, bool matchWidth = false, float matchWidthDiff = 0f)
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
                                vlayout.sizeDelta = new Vector2(rectTransform.rect.width, 0f);
                                vlayout.anchorMin = new Vector2(0f, 1f);
                                vlayout.anchorMax = new Vector2(1f, 1f);
                            }
                            if (matchWidth)
                                vlayout.sizeDelta = new Vector2(layout.Peek().rect.width-matchWidthDiff, 0f);

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
                            l.childForceExpandHeight = false;
                            l.childForceExpandWidth = isDoc;
                            l.spacing = spacing;

                            if (isDoc)
                                vlayout.anchoredPosition = new Vector2(0f, -vlayout.rect.height);

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

                    void ThematicBreak(float spacerSize = 1.5f)
                    { // TODO: Fix positioning
                        var go = new GameObject("ThematicBreak", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                        var vlayout = go.GetComponent<RectTransform>();
                        vlayout.SetParent(layout.Peek());
                        var l = go.GetComponent<HorizontalLayoutGroup>();
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
                        l.childAlignment = TextAnchor.UpperCenter;
                        l.spacing = 0f;

                        vlayout.localScale = Vector3.one;
                        vlayout.anchoredPosition = Vector2.zero;
                        vlayout.anchorMin = new Vector2(.5f, .5f);
                        vlayout.anchorMax = new Vector2(.5f, .5f);
                        vlayout.sizeDelta = new Vector2(layout.Peek().rect.width, BreakHeight);
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
                        le.minWidth = le.preferredWidth = layout.Peek().rect.width;
                        le.minHeight = le.preferredHeight = BreakHeight;
                        le.flexibleHeight = le.flexibleWidth = 1f;
                        rt.localScale = Vector3.one;
                        rt.localPosition = Vector3.zero;
                        rt.anchoredPosition = Vector3.zero;
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.sizeDelta = new Vector2(layout.Peek().rect.width, BreakHeight);

                        if (spacerSize != 0f)
                            Spacer(spacerSize);
                    }

                    switch (block.Tag)
                    {
                        case BlockTag.Document:
                            BlockNode("DocumentRoot", .5f, true, isDoc: true);
                            break;
                        case BlockTag.SetextHeading:
                            var l = BlockNode("SeHeading", 0f, false, t => t.hData = block.Heading, spacer: 0f);
                            if (l)
                            {
                                l.childAlignment = TextAnchor.UpperCenter;
                                l.padding = new RectOffset(TextInset, TextInset, 0, 0);
                            }
                            else
                                ThematicBreak(2f);
                            break;
                        case BlockTag.AtxHeading:
                                l = BlockNode("AtxHeading", .1f, false, t => t.hData = block.Heading);
                            if (l) l.padding = new RectOffset(TextInset, TextInset, 0, 0);
                            if (l && block.Heading.Level == 1)
                                l.childAlignment = TextAnchor.UpperCenter;
                            break;
                        case BlockTag.Paragraph:
                                l = BlockNode("Paragraph", .1f, false, spacer: 1.5f);
                            if (l) l.padding = new RectOffset(TextInset, TextInset, 0, 0);
                            break;
                        case BlockTag.ThematicBreak:
                            ThematicBreak();
                            break;
                        // TODO: add the rest of the tag types
                        case BlockTag.BlockQuote:
                            l = BlockNode("BlockQuote", .1f, true, matchWidth: true, matchWidthDiff: BlockQuoteInset*2, spacer: 1.5f);
                            if (l)
                            {
                                l.childForceExpandWidth = true;
                                l.padding = new RectOffset(BlockQuoteInset, BlockQuoteInset, BlockQuoteInset, 0);
                                var go = l.gameObject;

                                var im = go.AddComponent<Image>();
                                im.material = CustomUI.Utilities.UIUtilities.NoGlowMaterial;
                                im.type = Image.Type.Sliced;
                                im.sprite = Instantiate(BlockQuoteBackground);
                                im.color = BlockQuoteColor;
                            }
                            break;
                        case BlockTag.IndentedCode:
                        case BlockTag.FencedCode:
                            {
                                currentText = null;
                                var go = new GameObject("CodeBlock", typeof(RectTransform));
                                var vlayout = go.GetComponent<RectTransform>();
                                vlayout.SetParent(layout.Peek());

                                vlayout.anchorMin = new Vector2(.5f, .5f);
                                vlayout.anchorMax = new Vector2(.5f, .5f);
                                vlayout.localScale = Vector3.one;
                                vlayout.localPosition = Vector3.zero;
                                vlayout.sizeDelta = new Vector2(layout.Peek().rect.width - BlockCodeInset * 2, 0f);

                                var tt = go.AddComponent<TagTypeComponent>();
                                tt.Tag = block.Tag;

                                l = go.AddComponent<VerticalLayoutGroup>();

                                l.childControlHeight = l.childControlWidth = true;
                                l.childForceExpandHeight = false;
                                l.childForceExpandWidth = true;
                                l.spacing = 1.5f;
                                l.padding = new RectOffset(BlockCodeInset, BlockCodeInset, BlockCodeInset, BlockCodeInset);

                                var im = go.AddComponent<Image>();
                                im.material = CustomUI.Utilities.UIUtilities.NoGlowMaterial;
                                im.type = Image.Type.Sliced;
                                im.sprite = Instantiate(BlockQuoteBackground);
                                im.color = BlockCodeColor;

                                var text = BeatSaberUI.CreateText(vlayout, $"<noparse>{block.StringContent}</noparse>", Vector2.zero);
                                text.fontSize = BlockCodeSize;
                                text.font = Consolas;
                            }
                            break;

                        case BlockTag.List:
                            l = BlockNode("List", .05f, true, t => t.lData = block.ListData, matchWidth: true, matchWidthDiff: ListInset * 2, spacer: 1.5f);
                            if (l)
                            {
                                l.childForceExpandWidth = true;
                                l.padding = new RectOffset(ListInset, ListInset, 0, 0);
                                var go = l.gameObject;
                                var tt = go.GetComponent<TagTypeComponent>();

                                // count up children
                                var count = 0;
                                for (var c = block.FirstChild; c != null; c = c.NextSibling) count++;
                                tt.listCount = count;
                                tt.listIndex = 0;
                            }
                            break;
                        case BlockTag.ListItem:
                            l = BlockNode("ListItem", .05f, false, matchWidth: true, spacer: null);
                            if (l)
                            { // TODO: this is mega scuffed
                                l.childForceExpandWidth = true;
                                var go = l.gameObject;
                                var rt = go.GetComponent<RectTransform>();
                                var tt = go.GetComponent<TagTypeComponent>();
                                var ptt = rt.parent.gameObject.GetComponent<TagTypeComponent>();

                                var index = ptt.listIndex++;

                                var listCount = ptt.listCount;
                                var maxNum = listCount + ptt.lData.Start;
                                var numChars = (int)Math.Floor(Math.Log10(maxNum) + 1);

                                var cNum = index + ptt.lData.Start;

                                var lt = ptt.lData.ListType;

                                var id = lt == ListType.Bullet ? ptt.lData.BulletChar.ToString() : (cNum + (ptt.lData.Delimiter == ListDelimiter.Parenthesis ? ")" : "."));
                                var ident = BeatSaberUI.CreateText(rt, $"<nobr>{id} </nobr>\n", Vector2.zero);
                                if (lt == ListType.Ordered) // pad it out to fill space
                                    ident.text += $"<nobr><mspace=1em>{new string(' ', numChars + 1)}</mspace></nobr>";

                                var contGo = new GameObject("Content", typeof(RectTransform));
                                var vlayout = contGo.GetComponent<RectTransform>();
                                vlayout.SetParent(rt);

                                vlayout.anchorMin = new Vector2(.5f, .5f);
                                vlayout.anchorMax = new Vector2(.5f, .5f);
                                vlayout.localScale = Vector3.one;
                                vlayout.localPosition = Vector3.zero;
                                //vlayout.sizeDelta = new Vector2(rt.rect.width, 0f);

                                var tt2 = contGo.AddComponent<TagTypeComponent>();
                                tt2.Tag = block.Tag;

                                var csf = contGo.AddComponent<ContentSizeFitter>();
                                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                                l = contGo.AddComponent<VerticalLayoutGroup>();
                                l.childAlignment = TextAnchor.UpperLeft;
                                l.childControlHeight = l.childControlWidth = true;
                                l.childForceExpandHeight = false;
                                l.childForceExpandWidth = true;
                                l.spacing = .5f;

                                layout.Push(vlayout);
                            }
                            else
                                layout.Pop(); // pop one more to clear content rect from stack
                            break;

                        case BlockTag.HtmlBlock:
                            break;

                        case BlockTag.ReferenceDefinition: // i have no idea what the state looks like here
                            break;
                    }
                }
                else if (node.Inline != null)
                { // inline element
                    var inl = node.Inline;

                    void Flag(CurrentTextFlags flag)
                    {
                        if (node.IsOpening)
                            textFlags |= flag;
                        else if (node.IsClosing)
                            textFlags &= ~flag;
                    }

                    void EnsureText()
                    {
                        if (currentText == null)
                        {
                            Logger.md.Debug($"Adding new text element");

                            var tt = layout.Peek().gameObject.GetComponent<TagTypeComponent>();
                            currentText = BeatSaberUI.CreateText(layout.Peek(), "", Vector2.zero);
                            currentText.gameObject.AddComponent<TextLinkDecoder>();

                            currentText.enableWordWrapping = true;

                            switch (tt.Tag)
                            {
                                case BlockTag.List:
                                case BlockTag.ListItem:
                                case BlockTag.Paragraph:
                                    currentText.fontSize = PSize;
                                    break;
                                case BlockTag.AtxHeading:
                                    var size = H1Size;
                                    size -= HLevelDecrease * (tt.hData.Level - 1);
                                    currentText.fontSize = size;
                                    if (tt.hData.Level == 1)
                                        currentText.alignment = TextAlignmentOptions.Center;
                                    break;
                                case BlockTag.SetextHeading:
                                    currentText.fontSize = H1Size;
                                    currentText.alignment = TextAlignmentOptions.Center;
                                    break;
                                    // TODO: add other relevant types
                            }

                            texts.Add(currentText);
                        }
                    }
                    switch (inl.Tag)
                    {
                        case InlineTag.String:
                            EnsureText();

                            string head = "<noparse>", tail = "</noparse>";
                            if (textFlags.HasFlag(CurrentTextFlags.Bold))
                            { head = "<b>" + head; tail += "</b>"; }
                            if (textFlags.HasFlag(CurrentTextFlags.Italic))
                            { head = "<i>" + head; tail += "</i>"; }
                            if (textFlags.HasFlag(CurrentTextFlags.Strikethrough))
                            { head = "<s>" + head; tail += "</s>"; }
                            if (textFlags.HasFlag(CurrentTextFlags.Underline))
                            { head = "<u>" + head; tail += "</u>"; }

                            currentText.text += head + inl.LiteralContent + tail;
                            break;
                        case InlineTag.Strong:
                            Flag(CurrentTextFlags.Bold);
                            break;
                        case InlineTag.Strikethrough:
                            Flag(CurrentTextFlags.Strikethrough);
                            break;
                        case InlineTag.Emphasis:
                            Flag(CurrentTextFlags.Italic);
                            break;
                        case InlineTag.Code:
                            EnsureText();
                            currentText.text += $"<font=\"CONSOLAS\"><size=80%><mark=#A0A0C080><noparse>{inl.LiteralContent}</noparse></mark></size></font>";
                            break;
                        case InlineTag.SoftBreak:
                            EnsureText();
                            currentText.text += " "; // soft breaks translate to a space
                            break;
                        case InlineTag.LineBreak:
                            EnsureText();
                            currentText.text += "\n"; // line breaks translate to a new line
                            break;
                        case InlineTag.Link:
                            EnsureText();
                            Flag(CurrentTextFlags.Underline);

                            var color = LinkDefaultColor;
                            var targetUrl = ResolveUri(inl.TargetUrl);

                            var m = linkRefTitleColorRegex.Match(inl.LiteralContent);
                            if (m.Success)
                                color = "#" + m.Groups[1].Value;

                            if (node.IsOpening)
                                currentText.text += $"<color={color}><link=\"{targetUrl}\">";
                            else if (node.IsClosing)
                                currentText.text += "</link></color>";
                            break;
                        case InlineTag.RawHtml:
                            EnsureText();
                            currentText.text += inl.LiteralContent;
                            break;
                        case InlineTag.Placeholder:

                            break;
                    }
                }
            }

            yield return null; // delay one frame
             
            scrView.Setup();

            // this is the bullshit I have to use to make it work properly
            content.gameObject.GetComponent<VerticalLayoutGroup>().enabled = false;
            var childRt = content.GetChild(0) as RectTransform;
            childRt.anchoredPosition = new Vector2(0f, childRt.anchoredPosition.y);
        }

        private class TextLinkDecoder : MonoBehaviour, IPointerClickHandler
        {
            private TextMeshProUGUI tmp;

            public void Awake()
            {
                tmp = GetComponent<TextMeshProUGUI>();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                // this may not actually get me what i want
                int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, eventData.pointerPressRaycast.worldPosition, null);
                if (linkIndex != -1)
                { // was a link clicked?
                    TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];

                    // open the link id as a url, which is the metadata we added in the text field
                    var qualifiedUrl = linkInfo.GetLinkID();
                    if (qualifiedUrl.StartsWith("$$"))
                        return; // this means its used for something else

                    Logger.md.Debug($"Link pressed {qualifiedUrl}");

                    var uri = GetLinkUri(qualifiedUrl);
                    if (uri != null)
                        Process.Start(uri);
                }
            }

            private List<Color32[]> SetLinkToColor(int linkIndex, Color32 color)
            {
                TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];

                var oldVertColors = new List<Color32[]>(); // store the old character colors

                for (int i = 0; i < linkInfo.linkTextLength; i++)
                { // for each character in the link string
                    int characterIndex = linkInfo.linkTextfirstCharacterIndex + i; // the character index into the entire text
                    var charInfo = tmp.textInfo.characterInfo[characterIndex];
                    int meshIndex = charInfo.materialReferenceIndex; // Get the index of the material / sub text object used by this character.
                    int vertexIndex = charInfo.vertexIndex; // Get the index of the first vertex of this character.

                    Color32[] vertexColors = tmp.textInfo.meshInfo[meshIndex].colors32; // the colors for this character
                    oldVertColors.Add(vertexColors.ToArray());

                    if (charInfo.isVisible)
                    {
                        vertexColors[vertexIndex + 0] = color;
                        vertexColors[vertexIndex + 1] = color;
                        vertexColors[vertexIndex + 2] = color;
                        vertexColors[vertexIndex + 3] = color;
                    }
                }

                // Update Geometry
                tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

                return oldVertColors;
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
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }
            Clear(content);
            content.gameObject.SetActive(true);
        }
    }
}
