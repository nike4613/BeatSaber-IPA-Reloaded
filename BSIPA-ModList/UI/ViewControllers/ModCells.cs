using CustomUI.BeatSaber;
using CustomUI.Utilities;
using IPA.Loader;
using IPA.Loader.Features;
using IPA.Old;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BSIPA_ModList.UI.ViewControllers
{
    internal interface IClickableCell
    {
        void OnSelect(ModListController cntrl);
    }

    internal class BSIPAModCell : CustomCellInfo, IClickableCell
    {
        internal PluginLoader.PluginInfo Plugin;
        private ModListController list;

        public BSIPAModCell(ModListController list, PluginLoader.PluginInfo plugin)
            : base($"{plugin.Metadata.Name} <size=60%>v{plugin.Metadata.Version}", plugin.Metadata.Manifest.Author, null)
        {
            Plugin = plugin;
            this.list = list;

            if (string.IsNullOrWhiteSpace(subtext))
                subtext = "<color=#BFBFBF><i>Unspecified Author</i>";

            icon = plugin.Metadata.GetIcon();

            Logger.log.Debug($"BSIPAModCell {plugin.Metadata.Name} {plugin.Metadata.Version}");
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected BSIPAModCell {Plugin.Metadata.Name} {Plugin.Metadata.Version}");

            if (infoView == null)
            {
                var desc = Plugin.Metadata.Manifest.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "<color=#BFBFBF><i>No description</i>";

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Metadata.Name, "v" + Plugin.Metadata.Version.ToString(), subtext,
                    desc, Plugin.Metadata.Features.FirstOrDefault(f => f is NoUpdateFeature) != null ? Plugin.Metadata : null,
                    Plugin.Metadata.Manifest.Links);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }
    }

    internal class BSIPAIgnoredModCell : CustomCellInfo, IClickableCell
    {
        internal PluginLoader.PluginMetadata Plugin;
        private ModListController list;

        private const string authorFormat = "{0}  <color=#BFBFBF>- <i>Not loaded</i>";

        private string authorText;

        public BSIPAIgnoredModCell(ModListController list, PluginLoader.PluginMetadata plugin)
            : base($"<color=#878787>{plugin.Name} <size=60%>v{plugin.Version}", "", Utilities.DefaultBSIPAIcon)
        {
            Plugin = plugin;
            this.list = list;

            if (string.IsNullOrWhiteSpace(plugin.Manifest.Author))
                authorText = "<color=#BFBFBF><i>Unspecified Author</i>";
            else
                authorText = plugin.Manifest.Author;
            subtext = string.Format(authorFormat, authorText);

            Logger.log.Debug($"BSIPAIgnoredModCell {plugin.Name} {plugin.Version}");
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected BSIPAIgnoredModCell {Plugin.Name} {Plugin.Version}");

            if (infoView == null)
            {
                var desc = Plugin.Manifest.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "<color=#BFBFBF><i>No description</i>";

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Name, "v" + Plugin.Version.ToString(), authorText,
                    desc, Plugin.Features.FirstOrDefault(f => f is NoUpdateFeature) != null ? Plugin : null,
                    Plugin.Manifest.Links);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }
    }
    internal class LibraryModCell : CustomCellInfo, IClickableCell
    {
        internal PluginLoader.PluginInfo Plugin;
        private ModListController list;

        public LibraryModCell(ModListController list, PluginLoader.PluginInfo plugin)
            : base($"{plugin.Metadata.Name} <size=60%>v{plugin.Metadata.Version}", plugin.Metadata.Manifest.Author, null)
        {
            Plugin = plugin;
            this.list = list;

            if (string.IsNullOrWhiteSpace(subtext))
                subtext = "<color=#BFBFBF><i>Unspecified Author</i>";

            icon = Utilities.DefaultLibraryIcon;

            Logger.log.Debug($"LibraryModCell {plugin.Metadata.Name} {plugin.Metadata.Version}");
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected LibraryModCell {Plugin.Metadata.Name} {Plugin.Metadata.Version}");

            if (infoView == null)
            {
                var desc = Plugin.Metadata.Manifest.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "<color=#BFBFBF><i>No description</i>";

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Metadata.Name, "v" + Plugin.Metadata.Version.ToString(), subtext,
                    desc, Plugin.Metadata.Features.FirstOrDefault(f => f is NoUpdateFeature) != null ? Plugin.Metadata : null,
                    Plugin.Metadata.Manifest.Links);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }
    }

#pragma warning disable CS0618
    internal class IPAModCell : CustomCellInfo, IClickableCell
    {
        internal IPlugin Plugin;
        private ModListController list;

        public IPAModCell(ModListController list, IPlugin plugin)
            : base($"{plugin.Name} <size=60%>{plugin.Version}", "<color=#BFBFBF><i>Legacy</i>", Utilities.DefaultIPAIcon)
        {
            Plugin = plugin;
            this.list = list;

            Logger.log.Debug($"IPAModCell {plugin.Name} {plugin.Version}");
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected IPAModCell {Plugin.Name} {Plugin.Version}");

            if (infoView == null)
            {
                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Name, "v" + Plugin.Version.ToString(), "<color=#BFBFBF><i>Unknown Author</i>",
                    "<color=#A0A0A0>This mod was written for IPA Reloaded. No metadata is avaliable for this mod. " +
                    "Please contact the mod author and ask them to port it to BSIPA to provide more information.", null);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }
    }
#pragma warning restore

}
