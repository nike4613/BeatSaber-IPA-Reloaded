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
        public override TableCell CellForIdx(int idx)
        {
            var cell = base.CellForIdx(idx) as LevelListTableCell;
            var nameText = cell.GetPrivateField<TextMeshProUGUI>("_songNameText");
            nameText.overflowMode = TextOverflowModes.Overflow;
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
        }
    }
}
