using CustomUI.Utilities;
using IPA.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using System.Runtime.CompilerServices;
using IPA.Utilities;

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

        public static void DebugPrintTo<T>(this T obj, Action<string> log, int maxDepth = -1) =>
            DebugPrintTo(obj?.GetType() ?? typeof(T), obj, log, "", new ConditionalWeakTable<object, Ref<bool>>(), maxDepth);

        private static void DebugPrintTo(Type type, object obj, Action<string> log, string indent, ConditionalWeakTable<object, Ref<bool>> table, int maxDepth)
        {
            if (maxDepth == 0)
            {
                log(indent + "<Max depth reached>");
                return;
            }

            if (obj == null)
            {
                log(indent + "null");
                return;
            }

            table.Add(obj, true);

            if (type.IsPrimitive)
            {
                log(indent + obj.ToString());
                return;
            }
            if (type.IsEnum)
            {
                log(indent + obj.ToString());
                return;
            }
            if (type == typeof(string))
            {
                log(indent + $"\"{obj.ToString()}\"");
                return;
            }
            if (type.IsArray)
            {
                log(indent + $"{type.GetElementType()} [");
                foreach (var o in obj as Array)
                {
                    if (type.GetElementType().IsPrimitive)
                        log(indent + "- " + o?.ToString() ?? "null");
                    else if (type.GetElementType().IsEnum)
                        log(indent + "- " + o?.ToString() ?? "null");
                    else if (type.GetElementType() == typeof(string))
                        log(indent + "- " + $"\"{o?.ToString()}\"");
                    else
                    {
                        log(indent + $"- {o?.GetType()?.ToString() ?? "null"}");
                        if (o != null)
                        {
                            if (!table.TryGetValue(o, out _))
                                DebugPrintTo(o.GetType(), o, log, indent + "  ", table, maxDepth - 1);
                            else
                                log(indent + "  <Already printed>");
                        }
                    }
                }
                log(indent + "]");
                return;
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);

                if (field.FieldType.IsPrimitive)
                    log(indent + field.Name + ": " + value?.ToString() ?? "null");
                else if (field.FieldType.IsEnum)
                    log(indent + field.Name + ": " + value?.ToString() ?? "null");
                else if (field.FieldType == typeof(string))
                    log(indent + field.Name + ": " + $"\"{value?.ToString()}\"");
                else
                {
                    log(indent + field.Name + ": " + value?.GetType()?.ToString() ?? "null");
                    if (value != null)
                    {
                        if (!table.TryGetValue(value, out _))
                            DebugPrintTo(value?.GetType() ?? field.FieldType, value, log, indent + "  ", table, maxDepth - 1);
                        else
                            log(indent + "  <Already printed>");
                    }
                }
            }
        }
    }
}
