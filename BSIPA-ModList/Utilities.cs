using CustomUI.Utilities;
using IPA.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BSIPA_ModList
{
    internal static class Utilities
    {
        private static Sprite _defaultBsipaIcon;
        public static Sprite DefaultBSIPAIcon
        {
            get
            {
                if (_defaultBsipaIcon == null)
                    _defaultBsipaIcon = UIUtilities.LoadSpriteFromResources("BSIPA_ModList.Icons.mod_bsipa.png");
                return _defaultBsipaIcon;
            }
        }

        private static Sprite _defaultLibraryIcon;
        public static Sprite DefaultLibraryIcon
        {
            get
            {
                if (_defaultLibraryIcon == null)
                    _defaultLibraryIcon = UIUtilities.LoadSpriteFromResources("BSIPA_ModList.Icons.library.png");
                return _defaultLibraryIcon;
            }
        }

        private static Sprite _defaultIpaIcon;
        public static Sprite DefaultIPAIcon
        {
            get
            {
                if (_defaultIpaIcon == null)
                    _defaultIpaIcon = UIUtilities.LoadSpriteFromResources("BSIPA_ModList.Icons.mod_ipa.png");
                return _defaultIpaIcon;
            }
        }

        public static Sprite GetIcon(this PluginLoader.PluginMetadata meta)
        {
            if (meta == null) return DefaultBSIPAIcon;
            if (meta.IsBare) return DefaultLibraryIcon;
            else return GetEmbeddedIcon(meta) ?? DefaultBSIPAIcon;
        }

        private static Dictionary<PluginLoader.PluginMetadata, Sprite> embeddedIcons = new Dictionary<PluginLoader.PluginMetadata, Sprite>();
        public static Sprite GetEmbeddedIcon(this PluginLoader.PluginMetadata meta)
        {
            if (embeddedIcons.TryGetValue(meta, out var sprite)) return sprite;
            var icon = GetEmbeddedIconNoCache(meta);
            embeddedIcons.Add(meta, icon);
            return icon;
        }
        private static Sprite GetEmbeddedIconNoCache(PluginLoader.PluginMetadata meta)
        {
            if (meta.Assembly == null) return null;
            if (meta.Manifest.IconPath == null) return null;

            try
            {
                return UIUtilities.LoadSpriteRaw(UIUtilities.GetResource(meta.Assembly, meta.Manifest.IconPath));
            }
            catch (Exception e)
            {
                Logger.log.Error($"Error loading icon for {meta.Name}");
                Logger.log.Error(e);
                return null;
            }
        }
    }
}
