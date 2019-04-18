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

namespace BSIPA_ModList.UI
{
    internal class ModListController : CustomListViewController
    {
        private interface IClickableCell
        {
            void OnSelect(ModListController cntrl);
        }

        private class BSIPAModCell : CustomCellInfo, IClickableCell
        {
            private static Sprite _defaultIcon;
            public static Sprite DefaultIcon
            {
                get
                {
                    if (_defaultIcon == null)
                        _defaultIcon = UIUtilities.LoadSpriteFromResources("BSIPA_ModList.Icons.mod_bsipa.png");
                    return _defaultIcon;
                }
            }

            internal PluginLoader.PluginInfo Plugin;
            private ModListController list;

            public BSIPAModCell(ModListController list, PluginLoader.PluginInfo plugin) 
                : base($"{plugin.Metadata.Name} <size=60%>v{plugin.Metadata.Version}", plugin.Metadata.Manifest.Author, null)
            {
                Plugin = plugin;
                this.list = list;

                if (plugin.Metadata.Manifest.IconPath != null)
                    icon = UIUtilities.LoadSpriteRaw(UIUtilities.GetResource(plugin.Metadata.Assembly, plugin.Metadata.Manifest.IconPath));
                else
                    icon = DefaultIcon;

                Logger.log.Debug($"BSIPAModCell {plugin.Metadata.Name} {plugin.Metadata.Version}");
            }

            private ModInfoViewController infoView;

            public void OnSelect(ModListController cntrl)
            {
                Logger.log.Debug($"Selected BSIPAModCell {Plugin.Metadata.Name} {Plugin.Metadata.Version}");

                if (infoView == null)
                {
                    infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                    infoView.Init(icon, Plugin.Metadata.Name, "v" + Plugin.Metadata.Version.ToString(), Plugin.Metadata.Manifest.Author,
                        Plugin.Metadata.Manifest.Description, Plugin.Metadata.Features.FirstOrDefault(f => f is NoUpdateFeature) == null);
                }

                list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
            }
        }

        private class BSIPAIgnoredModCell : CustomCellInfo, IClickableCell
        {
            internal PluginLoader.PluginMetadata Plugin;
            private ModListController list;

            public BSIPAIgnoredModCell(ModListController list, PluginLoader.PluginMetadata plugin)
                : base($"<color=#878787>{plugin.Name} <size=60%>v{plugin.Version}", $"{plugin.Manifest.Author} <color=#BFBFBF>- <i>Not loaded</i>", BSIPAModCell.DefaultIcon)
            {
                Plugin = plugin;
                this.list = list;

                Logger.log.Debug($"BSIPAIgnoredModCell {plugin.Name} {plugin.Version}");
            }

            public void OnSelect(ModListController cntrl)
            {
                Logger.log.Debug($"Selected BSIPAIgnoredModCell {Plugin.Name} {Plugin.Version}");

                list.flow.ClearSelected();
            }
        }

#pragma warning disable CS0618
        private class IPAModCell : CustomCellInfo, IClickableCell
        {
            private static Sprite _defaultIcon;
            public static Sprite DefaultIcon
            {
                get
                {
                    if (_defaultIcon == null)
                        _defaultIcon = UIUtilities.LoadSpriteFromResources("BSIPA_ModList.Icons.mod_ipa.png");
                    return _defaultIcon;
                }
            }

            internal IPlugin Plugin;
            private ModListController list;

            public IPAModCell(ModListController list, IPlugin plugin)
                : base($"{plugin.Name} <size=60%>{plugin.Version}", "<color=#BFBFBF><i>Legacy</i>", DefaultIcon)
            {
                Plugin = plugin;
                this.list = list;

                Logger.log.Debug($"IPAModCell {plugin.Name} {plugin.Version}");
            }

            public void OnSelect(ModListController cntrl)
            {
                Logger.log.Debug($"Selected IPAModCell {Plugin.Name} {Plugin.Version}");

                list.flow.ClearSelected();
            }
        }
#pragma warning restore

        private ModListFlowCoordinator flow;

#pragma warning disable CS0618
        public void Init(ModListFlowCoordinator flow, IEnumerable<PluginLoader.PluginInfo> bsipaPlugins, IEnumerable<PluginLoader.PluginMetadata> ignoredPlugins, IEnumerable<IPlugin> ipaPlugins)
        {
            Logger.log.Debug("List Controller Init");

            DidActivateEvent = DidActivate;
            DidSelectRowEvent = DidSelectRow;

            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(.4f, 1f);

            includePageButtons = true;
            this.flow = flow;

            reuseIdentifier = "BSIPAModListTableCell";

            foreach (var plugin in bsipaPlugins)
                Data.Add(new BSIPAModCell(this, plugin));
            foreach (var plugin in ignoredPlugins)
                Data.Add(new BSIPAIgnoredModCell(this, plugin));
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
        }
    }
}
