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
    [RequireComponent(/*typeof(ScrollRect),*/ typeof(RectTransform))]
    public class MarkdownView : MonoBehaviour
    {
        private class TagTypeComponent : MonoBehaviour
        {
            internal BlockTag Tag;
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

        //private ScrollRect view;
        private RectTransform content;

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

                // check if its embedded
                if (HasEmbeddedImage != null && HasEmbeddedImage(arg))
                    return "!::" + arg;
                else
                    return "w::" + arg;
            }

            return arg;
        }

        protected void Awake()
        {
            content = new GameObject("Content Wrapper").AddComponent<RectTransform>();
            content.SetParent(transform);
            var contentLayout = content.gameObject.AddComponent<LayoutElement>();
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentLayout.preferredWidth = 100f; // to be adjusted
            content.sizeDelta = new Vector2(100f,100f);

            /*view = GetComponent<ScrollRect>();
            view.content = content;
            view.vertical = true;
            view.horizontal = false;
            view.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            view.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;*/
        }

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

                    void BlockNode(string name, float spacing, bool isVertical)
                    {
                        var type = isVertical ? typeof(VerticalLayoutGroup) : typeof(HorizontalLayoutGroup);
                        if (node.IsOpening)
                        {
                            Logger.md.Debug($"Creating block container {name}");

                            currentText = null;
                            var go = new GameObject(name, typeof(RectTransform), type);
                            var vlayout = go.GetComponent<RectTransform>();
                            vlayout.SetParent(layout.Peek());
                            go.AddComponent<TagTypeComponent>().Tag = block.Tag;
                            layout.Push(vlayout);

                            if (isVertical)
                            {
                                var vl = go.GetComponent<VerticalLayoutGroup>();
                                vl.childControlHeight = vl.childControlWidth =
                                    vl.childForceExpandHeight = vl.childForceExpandWidth = false;
                                vl.spacing = spacing;
                            }
                            else
                            {
                                var hl = go.GetComponent<HorizontalLayoutGroup>();
                                hl.childControlHeight = hl.childControlWidth =
                                    hl.childForceExpandHeight = hl.childForceExpandWidth = false;
                                hl.spacing = spacing;
                            }
                        }
                        else if (node.IsClosing)
                        {
                            currentText = null;
                            layout.Pop();
                        }
                    }

                    switch (block.Tag)
                    {
                        case BlockTag.Document:
                            BlockNode("DocumentRoot", 10f, true);
                            break;
                        case BlockTag.SetextHeading:
                            BlockNode("Heading1", .1f, false);
                            break;
                        case BlockTag.AtxHeading:
                            BlockNode("Heading2", .1f, false);
                            break;
                        case BlockTag.Paragraph:
                            BlockNode("Paragraph", .1f, false);
                            break;
                        // TODO: add the rest of the tag types
                    }
                }
                else if (node.Inline != null)
                { // inline element
                    var inl = node.Inline;

                    switch (inl.Tag)
                    {
                        case InlineTag.String:
                            if (currentText == null)
                            {
                                Logger.md.Debug($"Adding new text element");

                                var btt = layout.Peek().gameObject.GetComponent<TagTypeComponent>().Tag;
                                currentText = BeatSaberUI.CreateText(layout.Peek(), "", Vector2.zero);
                                //var le = currentText.gameObject.AddComponent<LayoutElement>();
                                
                                switch (btt)
                                {
                                    case BlockTag.List:
                                    case BlockTag.ListItem:
                                    case BlockTag.Paragraph:
                                        currentText.fontSize = 3.5f;
                                        currentText.enableWordWrapping = true;
                                        break;
                                    case BlockTag.AtxHeading:
                                        currentText.fontSize = 4f;
                                        currentText.enableWordWrapping = true;
                                        break;
                                    case BlockTag.SetextHeading:
                                        currentText.fontSize = 4.5f;
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
            foreach (Transform child in content)
                Destroy(child.gameObject);
        }
    }
}
