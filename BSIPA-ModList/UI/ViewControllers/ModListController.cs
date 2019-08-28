using System;
using System.Linq;
using System.Collections.Generic;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using HMUI;
using IPA.Loader;
using IPA.Old;
using UnityEngine;
using VRUI;
using IPA.Loader.Features;
using TMPro;
using BSIPA_ModList.UI.ViewControllers;
using UnityEngine.UI;

namespace BSIPA_ModList.UI
{
    internal class ModListController : CustomListViewController
    {
        public override TableCell CellForIdx(TableView view, int idx)
        {
            var cell = base.CellForIdx(view, idx) as LevelListTableCell;
            var nameText = cell.GetPrivateField<TextMeshProUGUI>("_songNameText");
            nameText.overflowMode = TextOverflowModes.Overflow;
            var authorText = cell.GetPrivateField<TextMeshProUGUI>("_authorText");
            authorText.overflowMode = TextOverflowModes.Overflow;
            return cell;
        }

        internal ModListFlowCoordinator flow;

#pragma warning disable CS0618
        public void Init(ModListFlowCoordinator flow, IEnumerable<PluginLoader.PluginMetadata> bsipaPlugins, IEnumerable<PluginLoader.PluginMetadata> ignoredPlugins, IEnumerable<IPlugin> ipaPlugins)
        {
            Data.Clear();

            Logger.log.Debug("List Controller Init");

            DidActivateEvent = DidActivate;
            DidSelectRowEvent = DidSelectRow;

            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(.4f, 1f);

            includePageButtons = true;
            this.flow = flow;

            reuseIdentifier = "BSIPAModListTableCell";

            foreach (var plugin in bsipaPlugins.Where(p => !p.IsBare))
                Data.Add(new BSIPAModCell(this, plugin));
            foreach (var plugin in ignoredPlugins)
                Data.Add(new BSIPAIgnoredModCell(this, plugin));
            foreach (var plugin in bsipaPlugins.Where(p => p.IsBare))
                Data.Add(new LibraryModCell(this, plugin));
            foreach (var plugin in ipaPlugins)
                Data.Add(new IPAModCell(this, plugin));
        }

#pragma warning restore

        public void Reload()
        {
            var cells = _customListTableView.GetPrivateField<List<TableCell>>("_visibleCells");
            foreach (var c in cells)
            {
                if (c == null) continue;
                c.gameObject?.SetActive(false);
                _customListTableView.AddCellToReusableCells(c);
            }
            cells.Clear();
            _customListTableView.RefreshCells(true);
        }

        private void DidSelectRow(TableView view, int index)
        {
            Debug.Assert(ReferenceEquals(view.dataSource, this));
            (Data[index] as IClickableCell)?.OnSelect(this);
        }

        private new void DidActivate(bool first, ActivationType type)
        {
            var rt = _customListTableView.transform as RectTransform;
            rt.anchorMin = new Vector2(.1f, 0f);
            rt.anchorMax = new Vector2(.9f, 1f);

            _customListTableView.gameObject.GetComponent<ScrollRect>().scrollSensitivity = 0f;

            var content = _customListTableView.contentTransform;
            content.anchoredPosition = new Vector2(7f, 0f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var cell in Data)
            {
                if (cell is IDisposable disp)
                    disp.Dispose();
            }
        }
    }
}
