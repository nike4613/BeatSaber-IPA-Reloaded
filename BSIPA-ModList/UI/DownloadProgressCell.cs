using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using System.Threading.Tasks;
using IPA.Updating.BeatMods;
using UnityEngine;

namespace BSIPA_ModList.UI
{

    // originally ripped verbatim from Andruzz's BeatSaverDownloader
    internal class DownloadProgressCell : LevelListTableCell
    {
        private DownloadObject mod;

        public void Init(DownloadObject mod)
        {
            Destroy(GetComponent<LevelListTableCell>());

            reuseIdentifier = "DownloadCell";

            this.mod = mod;

            _authorText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "Author");
            _authorText.enableWordWrapping = false;
            _authorText.overflowMode = TextOverflowModes.Overflow;
            _songNameText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "SongName");
            _songNameText.enableWordWrapping = false;
            _songNameText.overflowMode = TextOverflowModes.Overflow;
            _coverRawImage = GetComponentsInChildren<UnityEngine.UI.RawImage>().First(x => x.name == "CoverImage");
            _bgImage = GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name == "BG");
            _highlightImage = GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name == "Highlight");
            _beatmapCharacteristicAlphas = new float[0];
            _beatmapCharacteristicImages = new UnityEngine.UI.Image[0];
            _bought = true;

            foreach (var icon in GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")))
                Destroy(icon.gameObject);

            _songNameText.text = $"{mod.Mod.Name} <size=60%>v{mod.Mod.ResolvedVersion}</size>";
            _authorText.text = "";
            _coverRawImage.texture = mod.Icon.texture;

            _bgImage.enabled = true;
            _bgImage.sprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.one / 2f);
            _bgImage.type = UnityEngine.UI.Image.Type.Filled;
            _bgImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;

            Update();
        }

        public void Update()
        {
            if (mod == null) return;
            _bgImage.enabled = true;
            switch (mod.State)
            {
                case DownloadObject.States.ToDownload:
                    {
                        _bgImage.color = new Color(1f, 1f, 1f, 0.35f);
                        _bgImage.fillAmount = 0;
                    }
                    break;
                case DownloadObject.States.Downloading:
                    {
                        _bgImage.color = new Color(1f, 1f, 1f, 0.35f);
                        _bgImage.fillAmount = (float)mod.Progress;
                    }
                    break;
                case DownloadObject.States.Installing:
                    {
                        _bgImage.color = new Color(0f, 1f, 1f, 0.35f);
                        _bgImage.fillAmount = 1f;
                    }
                    break;
                case DownloadObject.States.Completed:
                    {
                        _bgImage.color = new Color(0f, 1f, 0f, 0.35f);
                        _bgImage.fillAmount = 1f;
                    }
                    break;
                case DownloadObject.States.Failed:
                    {
                        _bgImage.color = new Color(1f, 0f, 0f, 0.35f);
                        _bgImage.fillAmount = 1f;
                    }
                    break;
            }
        }
    }

}
