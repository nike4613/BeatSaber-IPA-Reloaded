using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IllusionPlugin
{
    /// <summary>
    /// Allows to get and set preferences for your mod. 
    /// </summary>
    public class ModPrefs {
        internal static Dictionary<IBeatSaberPlugin, ModPrefs> ModPrefses { get; set; } = new Dictionary<IBeatSaberPlugin, ModPrefs>();

        private IniFile Instance;

        /// <summary>
        /// Constructs a ModPrefs object for the provide plugin.
        /// </summary>
        /// <param name="plugin">the plugin to get the preferences file for</param>
        public ModPrefs(IBeatSaberPlugin plugin) {
            Instance = new IniFile(Path.Combine(Environment.CurrentDirectory, $"UserData/ModPrefs/{plugin.Name}.ini"));
            ModPrefses.Add(plugin, this);
        }

        /// <summary>
        /// Gets a string from the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="defaultValue">Value that should be used when no value is found.</param>
        /// <param name="autoSave">Whether or not the default value should be written if no value is found.</param>
        /// <returns></returns>
        public string GetString(string section, string name, string defaultValue = "", bool autoSave = false)
        {
            var value = Instance.IniReadValue(section, name);
            if (value != "")
                return value;
            else if (autoSave)
                SetString(section, name, defaultValue);

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
        public int GetInt(string section, string name, int defaultValue = 0, bool autoSave = false)
        {
            if (int.TryParse(Instance.IniReadValue(section, name), out var value))
                return value;
            else if (autoSave)
                SetInt(section, name, defaultValue);
                
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
        public float GetFloat(string section, string name, float defaultValue = 0f, bool autoSave = false)
        {
            if (float.TryParse(Instance.IniReadValue(section, name), out var value))
                return value;
            else if (autoSave)
                SetFloat(section, name, defaultValue);

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
        public bool GetBool(string section, string name, bool defaultValue = false, bool autoSave = false)
        {
            string sVal = GetString(section, name, null);
            if (sVal == "1" || sVal == "0")
            {
                return sVal == "1";
            } else if (autoSave)
            {
                SetBool(section, name, defaultValue);
            }

            return defaultValue;
        }


        /// <summary>
        /// Checks whether or not a key exists in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <returns></returns>
        public bool HasKey(string section, string name)
        {
            return Instance.IniReadValue(section, name) != null;
        }

        /// <summary>
        /// Sets a float in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public void SetFloat(string section, string name, float value)
        {
            Instance.IniWriteValue(section, name, value.ToString());
        }

        /// <summary>
        /// Sets an int in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public void SetInt(string section, string name, int value)
        {
            Instance.IniWriteValue(section, name, value.ToString());

        }

        /// <summary>
        /// Sets a string in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public void SetString(string section, string name, string value)
        {
            Instance.IniWriteValue(section, name, value);

        }

        /// <summary>
        /// Sets a bool in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public void SetBool(string section, string name, bool value)
        {
            Instance.IniWriteValue(section, name, value ? "1" : "0");

        }
        
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
        public static ModPrefs GetModPrefs(this IBeatSaberPlugin plugin) {
            return ModPrefs.ModPrefses.First(o => o.Key == plugin).Value;
        }
    }
}
