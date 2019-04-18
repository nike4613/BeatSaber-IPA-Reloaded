using CustomUI.BeatSaber;
using System;
using System.Collections.Generic;
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
        internal bool CanUpdate;

        private ModInfoView view;

        public void Init(Sprite icon, string name, string version, string author, string description, bool canUpdate)
        {
            Icon = icon;
            Name = name;
            Version = version;
            Author = author;
            Description = description;
            CanUpdate = canUpdate;

            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(0.4f, 1f);

            var go = new GameObject("Info View");
            go.SetActive(false);
            view = go.AddComponent<ModInfoView>();
            view.gameObject.AddComponent<RectMask2D>();
            view.Init(this);
            var rt = view.transform as RectTransform;
            rt.SetParent(transform);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(0.2f, 0f);
            go.SetActive(true);
        }
    }

    internal class ModInfoView : MonoBehaviour
    {
        private ModInfoViewController controller;

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI authorText;
        private TextMeshProUGUI descText;

        public void Init(ModInfoViewController controller)
        {
            this.controller = controller;

            var rectTransform = transform as RectTransform;
            rectTransform.sizeDelta = new Vector2(60f, 10f);

            titleText = BeatSaberUI.CreateText(rectTransform, $"{controller.Name} <size=60%>{controller.Version}", new Vector2(0f, 0f));
            titleText.rectTransform.anchorMin = new Vector2(0f, .8f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.fontSize = 6f;
            authorText = BeatSaberUI.CreateText(rectTransform, controller.Author, new Vector2(0f, 0f));
            titleText.rectTransform.anchorMin = new Vector2(0f, .6f);
            titleText.rectTransform.anchorMax = new Vector2(1f, .8f);
            authorText.fontSize = 3f;
            descText = BeatSaberUI.CreateText(rectTransform, controller.Description, new Vector2(0f, 0f));
            descText.rectTransform.anchorMin = new Vector2(0f, .0f);
            descText.rectTransform.anchorMax = new Vector2(1f, .6f);
        }

        public void OnUpdate()
        {
            var cpos = titleText.rectTransform.anchoredPosition;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                titleText.rectTransform.anchoredPosition = new Vector2(cpos.x - .1f, cpos.y);
            if (Input.GetKeyDown(KeyCode.RightArrow))
                titleText.rectTransform.anchoredPosition = new Vector2(cpos.x + .1f, cpos.y);
            if (Input.GetKeyDown(KeyCode.UpArrow))
                titleText.rectTransform.anchoredPosition = new Vector2(cpos.x, cpos.y + .1f);
            if (Input.GetKeyDown(KeyCode.DownArrow))
                titleText.rectTransform.anchoredPosition = new Vector2(cpos.x, cpos.y - .1f);
        }
    }
}
