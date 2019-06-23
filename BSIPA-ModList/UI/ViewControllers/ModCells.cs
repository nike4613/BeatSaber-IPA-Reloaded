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
        void Update();
    }

    internal class BSIPAModCell : CustomCellInfo, IClickableCell, IDisposable
    {
        internal PluginLoader.PluginMetadata Plugin;
        private ModListController list;

        private PluginManager.PluginEnableDelegate enableDel;
        private PluginManager.PluginDisableDelegate disableDel;

        public BSIPAModCell(ModListController list, PluginLoader.PluginMetadata plugin)
            : base("", "", null)
        {
            Plugin = plugin;
            this.list = list;

            var thisWeakRef = new WeakReference<BSIPAModCell>(this);
            PluginManager.PluginDisableDelegate reflessDDel = null;
            reflessDDel = disableDel = (p, r) => PluginManager_PluginDisabled(p, r, thisWeakRef, reflessDDel); // some indirection to make it a weak link for GC
            PluginManager.PluginDisabled += reflessDDel;
            PluginManager.PluginEnableDelegate reflessEDel = null;
            reflessEDel = enableDel = (p, r) => PluginManager_PluginEnabled(p, r, thisWeakRef, reflessEDel); // some indirection to make it a weak link for GC
            PluginManager.PluginEnabled += reflessEDel;

            Update(propogate: false);
        }

        private static void PluginManager_PluginEnabled(PluginLoader.PluginInfo plugin, bool needsRestart, WeakReference<BSIPAModCell> _self, PluginManager.PluginEnableDelegate ownDel)
        {
            if (!_self.TryGetTarget(out var self))
            {
                PluginManager.PluginEnabled -= ownDel;
                return;
            }

            if (plugin.Metadata != self.Plugin) return;

            self.Update(true, needsRestart);
        }

        private static void PluginManager_PluginDisabled(PluginLoader.PluginMetadata plugin, bool needsRestart, WeakReference<BSIPAModCell> _self, PluginManager.PluginDisableDelegate ownDel)
        {
            if (!_self.TryGetTarget(out var self))
            {
                PluginManager.PluginDisabled -= ownDel;
                return;
            }

            if (plugin != self.Plugin) return;

            self.Update(false, needsRestart);
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected BSIPAModCell {Plugin.Name} {Plugin.Version}");

            if (infoView == null)
            {
                var desc = Plugin.Manifest.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "*No description*";

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Name, "v" + Plugin.Version.ToString(), subtext,
                    desc, Plugin, Plugin.Manifest.Links, true, list.flow);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }

        void IClickableCell.Update() => Update(null, false, false);

        public void Update(bool? _enabled = null, bool needsRestart = false, bool propogate = true)
        {
            text = $"{Plugin.Name} <size=60%>v{Plugin.Version}";
            subtext = Plugin.Manifest.Author;

            if (string.IsNullOrWhiteSpace(subtext))
                subtext = "<color=#BFBFBF><i>Unspecified Author</i>";

            var enabled = _enabled ?? !PluginManager.IsDisabled(Plugin);
            if (!enabled)
                subtext += "  <color=#C2C2C2>- <i>Disabled</i>";
            if (needsRestart)
                subtext += " <i>(Restart to apply)</i>";

            icon = Plugin.GetIcon();

            var desc = Plugin.Manifest.Description;
            if (string.IsNullOrWhiteSpace(desc))
                desc = "*No description*";
            infoView?.Reload(Plugin.Name, "v" + Plugin.Version.ToString(), subtext, desc);

            if (propogate)
                list.Reload();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PluginManager.PluginDisabled -= disableDel;
                    PluginManager.PluginEnabled -= enableDel;
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
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
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected BSIPAIgnoredModCell {Plugin.Name} {Plugin.Version}");

            if (infoView == null)
            {
                var desc = Plugin.Manifest.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "*No description*";

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Name, "v" + Plugin.Version.ToString(), authorText,
                    desc, Plugin, Plugin.Manifest.Links);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }

        public void Update()
        {
        }
    }
    internal class LibraryModCell : CustomCellInfo, IClickableCell
    {
        internal PluginLoader.PluginMetadata Plugin;
        private ModListController list;

        public LibraryModCell(ModListController list, PluginLoader.PluginMetadata plugin)
            : base($"{plugin.Name} <size=60%>v{plugin.Version}", plugin.Manifest.Author, null)
        {
            Plugin = plugin;
            this.list = list;

            if (string.IsNullOrWhiteSpace(subtext))
                subtext = "<color=#BFBFBF><i>Unspecified Author</i></color>";

            icon = Utilities.DefaultLibraryIcon;
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected LibraryModCell {Plugin.Name} {Plugin.Version}");

            if (infoView == null)
            {
                var desc = Plugin.Manifest.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "*No description*";

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Name, "v" + Plugin.Version.ToString(), subtext,
                    desc, Plugin, Plugin.Manifest.Links);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }

        public void Update()
        {
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
        }

        private ModInfoViewController infoView;

        public void OnSelect(ModListController cntrl)
        {
            Logger.log.Debug($"Selected IPAModCell {Plugin.Name} {Plugin.Version}");

            if (infoView == null)
            {
                PluginLoader.PluginMetadata updateInfo = null;

                try
                {
                    updateInfo = new PluginLoader.PluginMetadata
                    {
                        Name = Plugin.Name,
                        Id = Plugin.Name,
                        Version = new SemVer.Version(Plugin.Version)
                    };
                }
                catch (Exception e)
                {
                    Logger.log.Warn($"Could not generate fake update info for {Plugin.Name}");
                    Logger.log.Warn(e);
                }

                infoView = BeatSaberUI.CreateViewController<ModInfoViewController>();
                infoView.Init(icon, Plugin.Name, "v" + Plugin.Version.ToString(), "<color=#BFBFBF><i>Unknown Author</i>",
                    "This mod was written for IPA.\n===\n\n## No metadata is avaliable for this mod.\n\n" +
                    "Please contact the mod author and ask them to port it to BSIPA to provide more information.", updateInfo);
            }

            list.flow.SetSelected(infoView, immediate: list.flow.HasSelected);
        }

        public void Update()
        {
        }
    }
#pragma warning restore

}
