using System;
using System.Collections.Generic;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using HMUI;
using IPA.Loader;
using IPA.Old;
using UnityEngine;

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

            public BSIPAModCell(PluginLoader.PluginInfo plugin) 
                : base($"{plugin.Metadata.Name} <size=60%>v{plugin.Metadata.Version}", plugin.Metadata.Manifest.Author, null)
            {
                Plugin = plugin;

                if (plugin.Metadata.Manifest.IconPath != null)
                    icon = UIUtilities.LoadSpriteRaw(UIUtilities.GetResource(plugin.Metadata.Assembly, plugin.Metadata.Manifest.IconPath));
                else
                    icon = DefaultIcon;

                Logger.log.Debug($"BSIPAModCell {plugin.Metadata.Name} {plugin.Metadata.Version}");
            }

            public void OnSelect(ModListController cntrl)
            {
                Logger.log.Debug($"Selected BSIPAModCell {Plugin.Metadata.Name} {Plugin.Metadata.Version}");
            }
        }

        private class BSIPAIgnoredModCell : CustomCellInfo, IClickableCell
        {
            internal PluginLoader.PluginMetadata Plugin;

            public BSIPAIgnoredModCell(PluginLoader.PluginMetadata plugin)
                : base($"<color=#878787>{plugin.Name} <size=60%>v{plugin.Version}", $"{plugin.Manifest.Author} <color=#BFBFBF>- <i>Not loaded</i>", BSIPAModCell.DefaultIcon)
            {
                Plugin = plugin;

                Logger.log.Debug($"BSIPAIgnoredModCell {plugin.Name} {plugin.Version}");
            }

            public void OnSelect(ModListController cntrl)
            {
                Logger.log.Debug($"Selected BSIPAIgnoredModCell {Plugin.Name} {Plugin.Version}");
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

            public IPAModCell(IPlugin plugin)
                : base($"{plugin.Name} <size=60%>{plugin.Version}", "<color=#BFBFBF><i>Legacy</i>", DefaultIcon)
            {
                Plugin = plugin;

                Logger.log.Debug($"IPAModCell {plugin.Name} {plugin.Version}");
            }

            public void OnSelect(ModListController cntrl)
            {
                Logger.log.Debug($"Selected IPAModCell {Plugin.Name} {Plugin.Version}");
            }
        }
#pragma warning restore

#pragma warning disable CS0618
        public void Init(IEnumerable<PluginLoader.PluginInfo> bsipaPlugins, IEnumerable<PluginLoader.PluginMetadata> ignoredPlugins, IEnumerable<IPlugin> ipaPlugins)
        {
            Logger.log.Debug("List Controller Init");

            DidActivateEvent = DidActivate;
            DidSelectRowEvent = DidSelectRow;

            includePageButtons = true;

            reuseIdentifier = "BSIPAModListTableCell";

            foreach (var plugin in bsipaPlugins)
                Data.Add(new BSIPAModCell(plugin));
            foreach (var plugin in ignoredPlugins)
                Data.Add(new BSIPAIgnoredModCell(plugin));
            foreach (var plugin in ipaPlugins)
                Data.Add(new IPAModCell(plugin));
        }

#pragma warning restore

        private void DidSelectRow(TableView view, int index)
        {
            Debug.Assert(ReferenceEquals(view.dataSource, this));
            (Data[index] as IClickableCell)?.OnSelect(this);
        }

        private new void DidActivate(bool first, ActivationType type)
        {

        }
    }
}
