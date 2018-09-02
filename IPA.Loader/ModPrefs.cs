using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IPA
{
    /// <summary>
    /// Allows to get and set preferences for your mod. 
    /// </summary>
    public interface IModPrefs
    {
        /// <summary>
        /// Gets a string from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        string GetString(string section, string name, string defaultValue = "", bool autoSave = false);
        /// <summary>
        /// Gets an int from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        int GetInt(string section, string name, int defaultValue = 0, bool autoSave = false);
        /// <summary>
        /// Gets a float from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        float GetFloat(string section, string name, float defaultValue = 0f, bool autoSave = false);
        /// <summary>
        /// Gets a bool from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        bool GetBool(string section, string name, bool defaultValue = false, bool autoSave = false);
        /// <summary>
        /// Checks whether or not a key exists in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <returns></returns>
        bool HasKey(string section, string name);
        /// <summary>
        /// Sets a float in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        void SetFloat(string section, string name, float value);
        /// <summary>
        /// Sets an int in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        void SetInt(string section, string name, int value);
        /// <summary>
        /// Sets a string in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        void SetString(string section, string name, string value);
        /// <summary>
        /// Sets a bool in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        void SetBool(string section, string name, bool value);
    }

    /// <summary>
    /// Allows to get and set preferences for your mod. 
    /// </summary>
    public class ModPrefs : IModPrefs
    {
        private static ModPrefs _staticInstance = null;
        private static IModPrefs StaticInstace
        {
            get
            {
                if (_staticInstance == null)
                    _staticInstance = new ModPrefs();
                return _staticInstance;
            }
        }

        internal static Dictionary<IBeatSaberPlugin, ModPrefs> ModPrefses { get; set; } = new Dictionary<IBeatSaberPlugin, ModPrefs>();

        private IniFile Instance;

        /// <summary>
        /// Constructs a ModPrefs object for the provide plugin.
        /// </summary>
        /// <param name="plugin">the plugin to get the preferences file for</param>
        public ModPrefs(IBeatSaberPlugin plugin) {
            Instance = new IniFile(Path.Combine(Environment.CurrentDirectory, "UserData", "ModPrefs", $"{plugin.Name}.ini"));
            ModPrefses.Add(plugin, this);
        }

        private ModPrefs()
        {
            Instance = new IniFile(Path.Combine(Environment.CurrentDirectory, "UserData", "modprefs.ini"));
        }

        string IModPrefs.GetString(string section, string name, string defaultValue, bool autoSave)
        {
            var value = Instance.IniReadValue(section, name);
            if (value != "")
                return value;
            else if (autoSave)
                (this as IModPrefs).SetString(section, name, defaultValue);

            return defaultValue;
        }
        /// <summary>
        /// Gets a string from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        public static string GetString(string section, string name, string defaultValue = "", bool autoSave = false)
            => StaticInstace.GetString(section, name, defaultValue, autoSave);

        int IModPrefs.GetInt(string section, string name, int defaultValue, bool autoSave)
        {
            if (int.TryParse(Instance.IniReadValue(section, name), out var value))
                return value;
            else if (autoSave)
                (this as IModPrefs).SetInt(section, name, defaultValue);
                
            return defaultValue;
        }
        /// <summary>
        /// Gets an int from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        public static int GetInt(string section, string name, int defaultValue = 0, bool autoSave = false)
            => StaticInstace.GetInt(section, name, defaultValue, autoSave);

        float IModPrefs.GetFloat(string section, string name, float defaultValue, bool autoSave)
        {
            if (float.TryParse(Instance.IniReadValue(section, name), out var value))
                return value;
            else if (autoSave)
                (this as IModPrefs).SetFloat(section, name, defaultValue);

            return defaultValue;
        }
        /// <summary>
        /// Gets a float from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        public static float GetFloat(string section, string name, float defaultValue = 0f, bool autoSave = false)
            => StaticInstace.GetFloat(section, name, defaultValue, autoSave);

        bool IModPrefs.GetBool(string section, string name, bool defaultValue, bool autoSave)
        {
            string sVal = GetString(section, name, null);
            if (sVal == "1" || sVal == "0")
            {
                return sVal == "1";
            } else if (autoSave)
            {
                (this as IModPrefs).SetBool(section, name, defaultValue);
            }

            return defaultValue;
        }
        /// <summary>
        /// Gets a bool from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        public static bool GetBool(string section, string name, bool defaultValue = false, bool autoSave = false)
            => StaticInstace.GetBool(section, name, defaultValue, autoSave);

        bool IModPrefs.HasKey(string section, string name)
        {
            return Instance.IniReadValue(section, name) != null;
        }
        /// <summary>
        /// Checks whether or not a key exists in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <returns></returns>
        public static bool HasKey(string section, string name) => StaticInstace.HasKey(section, name);

        void IModPrefs.SetFloat(string section, string name, float value)
        {
            Instance.IniWriteValue(section, name, value.ToString());
        }
        /// <summary>
        /// Sets a float in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetFloat(string section, string name, float value)
            => StaticInstace.SetFloat(section, name, value);

        void IModPrefs.SetInt(string section, string name, int value)
        {
            Instance.IniWriteValue(section, name, value.ToString());
        }
        /// <summary>
        /// Sets an int in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetInt(string section, string name, int value)
            => StaticInstace.SetInt(section, name, value);

        void IModPrefs.SetString(string section, string name, string value)
        {
            Instance.IniWriteValue(section, name, value);
        }
        /// <summary>
        /// Sets a string in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetString(string section, string name, string value)
            => StaticInstace.SetString(section, name, value);

        void IModPrefs.SetBool(string section, string name, bool value)
        {
            Instance.IniWriteValue(section, name, value ? "1" : "0");
        }
        /// <summary>
        /// Sets a bool in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetBool(string section, string name, bool value)
            => StaticInstace.SetBool(section, name, value);
    }
    
    /// <summary>
    /// An extension class for IBeatSaberPlugins.
    /// </summary>
    public static class ModPrefsExtensions {
        /// <summary>
        /// Gets the ModPrefs object for the provided plugin.
        /// </summary>
        /// <param name="plugin">the plugin wanting the prefrences</param>
        /// <returns>the ModPrefs object</returns>
        public static IModPrefs GetModPrefs(this IBeatSaberPlugin plugin) {
            return ModPrefs.ModPrefses.First(o => o.Key == plugin).Value;
        }
    }
}
