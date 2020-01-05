using System;
using System.Globalization;
using System.IO;
using IPA.Loader;
#if NET3
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Config
{
    /// <summary>
    /// Allows to get and set preferences for your mod. 
    /// </summary>
    [Obsolete("Uses IniFile, which uses 16 bit system calls. Use BS Utils INI system for now.")]
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

    /// <inheritdoc />
    /// <summary>
    /// Allows to get and set preferences for your mod. 
    /// </summary>
    [Obsolete("Uses IniFile, which uses 16 bit system calls. Use BS Utils INI system for now.")]
    public class ModPrefs : IModPrefs
    {
        private static ModPrefs _staticInstance;
        private static IModPrefs StaticInstance => _staticInstance ?? (_staticInstance = new ModPrefs());

        private readonly IniFile _instance;

        /// <summary>
        /// Constructs a ModPrefs object for the provide plugin.
        /// </summary>
        /// <param name="plugin">the plugin to get the preferences file for</param>
        public ModPrefs(PluginMetadata plugin) {
            _instance = new IniFile(Path.Combine(Environment.CurrentDirectory, "UserData", "ModPrefs",
                $"{plugin.Name}.ini"));
        }

        private ModPrefs()
        {
            _instance = new IniFile(Path.Combine(Environment.CurrentDirectory, "UserData", "modprefs.ini"));
        }

        string IModPrefs.GetString(string section, string name, string defaultValue, bool autoSave)
        {
            var value = _instance.IniReadValue(section, name);
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
            => StaticInstance.GetString(section, name, defaultValue, autoSave);

        int IModPrefs.GetInt(string section, string name, int defaultValue, bool autoSave)
        {
            if (int.TryParse(_instance.IniReadValue(section, name), out var value))
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
            => StaticInstance.GetInt(section, name, defaultValue, autoSave);

        float IModPrefs.GetFloat(string section, string name, float defaultValue, bool autoSave)
        {
            if (float.TryParse(_instance.IniReadValue(section, name), out var value))
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
            => StaticInstance.GetFloat(section, name, defaultValue, autoSave);

        bool IModPrefs.GetBool(string section, string name, bool defaultValue, bool autoSave)
        {
            string sVal = GetString(section, name, null);
            if (sVal == "1" || sVal == "0")
            {
                return sVal == "1";
            }
            else if (autoSave)
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
            => StaticInstance.GetBool(section, name, defaultValue, autoSave);

        bool IModPrefs.HasKey(string section, string name)
        {
            return (_instance.IniReadValue(section, name)?.Length ?? 0) > 0;
        }
        /// <summary>
        /// Checks whether or not a key exists in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <returns></returns>
        public static bool HasKey(string section, string name) => StaticInstance.HasKey(section, name);

        void IModPrefs.SetFloat(string section, string name, float value)
        {
            _instance.IniWriteValue(section, name, value.ToString(CultureInfo.InvariantCulture));
        }
        /// <summary>
        /// Sets a float in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetFloat(string section, string name, float value)
            => StaticInstance.SetFloat(section, name, value);

        void IModPrefs.SetInt(string section, string name, int value)
        {
            _instance.IniWriteValue(section, name, value.ToString());
        }
        /// <summary>
        /// Sets an int in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetInt(string section, string name, int value)
            => StaticInstance.SetInt(section, name, value);

        void IModPrefs.SetString(string section, string name, string value)
        {
            _instance.IniWriteValue(section, name, value);
        }
        /// <summary>
        /// Sets a string in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetString(string section, string name, string value)
            => StaticInstance.SetString(section, name, value);

        void IModPrefs.SetBool(string section, string name, bool value)
        {
            _instance.IniWriteValue(section, name, value ? "1" : "0");
        }
        /// <summary>
        /// Sets a bool in the ini.
        /// </summary>
        /// <param name="section">Section of the key.</param>
        /// <param name="name">Name of the key.</param>
        /// <param name="value">Value that should be written.</param>
        public static void SetBool(string section, string name, bool value)
            => StaticInstance.SetBool(section, name, value);
    }
}
