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
using IPA.Utilities;
using System.Reflection;
using UnityEngine.EventSystems;
using System.Diagnostics;
using System.Collections;

namespace BSIPA_ModList.UI.ViewControllers
{
    [RequireComponent(typeof(RectTransform))]
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

        private ScrollView scrView;
        private RectTransform content;
        private RectTransform viewport;

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

        private static AssetBundle _bundle;
        private static AssetBundle Bundle
        {
            get
            {
                if (_bundle == null)
                    _bundle = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("BSIPA_ModList.Bundles.consolas"));
                return _bundle;
            }
        }
        private static TMP_FontAsset _consolas;
        private static TMP_FontAsset Consolas
        {
            get
            {
                if (_consolas == null)
                {
                    _consolas = Bundle?.LoadAsset<TMP_FontAsset>("CONSOLAS");
                    if (_consolas != null)
                    {
                        _consolas.material.color = new Color(1f, 1f, 1f, 0f);
                        _consolas.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                }
                return _consolas;
            }
        }

        protected void Awake()
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
            scrView.SetPrivateField("_pageUpButton", pageUp);
            scrView.SetPrivateField("_pageDownButton", pageDown);
            scrView.SetPrivateField("_contentRectTransform", content);
            scrView.SetPrivateField("_viewport", viewport);

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
                StartCoroutine(UpdateMd());
        }

        [Flags]
        private enum CurrentTextFlags
        {
            None = 0, Bold = 1, Italic = 2, Underline = 4, Strikethrough = 8,
        }

        private const string LinkDefaultColor = "#0061ff";
        private const string LinkHoverColor = "#009dff";

        private bool resetContentPosition = false;
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
                                vlayout.sizeDelta = new Vector2(rectTransform.rect.width, 0f);
                                vlayout.anchorMin = new Vector2(0f, 1f);
                                vlayout.anchorMax = new Vector2(1f, 1f);
                                //vlayout.anchoredPosition = new Vector2(0f, -30f); // no idea where this -30 comes from, but it works for my use
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

                            if (isDoc)
                            {
                                vlayout.anchoredPosition = new Vector2(0f, -vlayout.rect.height);
                            }

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

                    void ThematicBreak()
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

                        Spacer(1f);
                    }

                    switch (block.Tag)
                    {
                        case BlockTag.Document:
                            BlockNode("DocumentRoot", .5f, true, isDoc: true);
                            break;
                        case BlockTag.SetextHeading:
                            var l = BlockNode("SeHeading", .1f, false, t => t.hData = block.Heading);
                            if (l)
                            {
                                l.childAlignment = TextAnchor.UpperCenter;
                                l.padding = new RectOffset(TextInset, TextInset, 0, 0);
                            }
                            else ThematicBreak();
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

                    const float PSize = 3.5f;
                    const float H1Size = 4.8f;
                    const float HLevelDecrease = 0.5f;
                    void EnsureText()
                    {
                        if (currentText == null)
                        {
                            Logger.md.Debug($"Adding new text element");

                            var tt = layout.Peek().gameObject.GetComponent<TagTypeComponent>();
                            currentText = BeatSaberUI.CreateText(layout.Peek(), "", Vector2.zero);
                            currentText.gameObject.AddComponent<TextLinkDecoder>();

                            /*if (Consolas != null)
                            {
                                // Set the font to Consolas so code blocks work
                                currentText.font = Instantiate(Consolas);
                                currentText.text = $"<font={DefaultFontName}>";
                            }*/

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
                            currentText.text += $"<link=\"$$codeBlock\"><noparse>{inl.LiteralContent}</noparse></link>";
                            break;
                        case InlineTag.Link:
                            EnsureText();
                            Flag(CurrentTextFlags.Underline);
                            if (node.IsOpening)
                                currentText.text += $"<color={LinkDefaultColor}><link=\"{ResolveUri(inl.TargetUrl)}\">";
                            else if (node.IsClosing)
                                currentText.text += "</link></color>";
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

            if (Consolas != null)
            {
                foreach (var link in texts.Select(t => t.textInfo.linkInfo).Aggregate<IEnumerable<TMP_LinkInfo>>(Enumerable.Concat).Where(l => l.GetLinkID() == "$$codeBlock"))
                {
                    //link.textComponent.font = Consolas;
                    var texinfo = link.textComponent.textInfo;
                    texinfo.characterInfo[link.linkTextfirstCharacterIndex].DebugPrintTo(Logger.md.Debug, 2);
                    for (int i = link.linkTextfirstCharacterIndex; i < link.linkTextfirstCharacterIndex + link.linkTextLength; i++)
                    {

                        texinfo.characterInfo[i].fontAsset = Consolas;
                        texinfo.characterInfo[i].material = Consolas.material;
                        texinfo.characterInfo[i].isUsingAlternateTypeface = true;
                    }
                }
                foreach (var text in texts)
                {
                    text.SetLayoutDirty();
                    text.SetVerticesDirty();
                }
            }
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
