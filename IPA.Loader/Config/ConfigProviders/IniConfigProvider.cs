using IPA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using IniParser;
using IniParser.Model;
using System.Reflection;

namespace IPA.Config.ConfigProviders
{
    [Config.Type("ini")]
    internal class IniConfigProvider : IConfigProvider
    {
        public static void RegisterConfig()
        {
            Config.Register<IniConfigProvider>();
        }

        private IniData _iniData;

        // TODO: create a wrapper that allows empty object creation
        public dynamic Dynamic => _iniData;


        public bool HasChanged { get; private set; }
        public bool InMemoryChanged { get; set; }

        public DateTime LastModified => File.GetLastWriteTime(Filename);

        private string _filename;

        public string Filename
        {
            get => _filename;
            set
            {
                if (_filename != null)
                    throw new InvalidOperationException("Can only assign to Filename once");
                _filename = value;
            }
        }

        // Load file
        public void Load()
        {
            Logger.config.Debug($"Loading file {Filename}");

            var fileInfo = new FileInfo(Filename);
            if (fileInfo.Exists)
            {
                try
                {
                    var parser = new FileIniDataParser();
                    parser.Parser.Configuration.CaseInsensitive = true;

                    _iniData = parser.ReadFile(fileInfo.FullName);
                }
                catch (Exception e)
                {
                    Logger.config.Error($"Error parsing INI in file {Filename}; resetting to empty INI");
                    Logger.config.Error(e);

                    _iniData = new IniData();
                }
            }
            else
            {
                Logger.config.Debug($"File {fileInfo.FullName} doesn't exist");
                _iniData = new IniData();
            }

            InMemoryChanged = true;
        }


        // This is basically trying to deserialize from INI data to a config object
        public T Parse<T>()
        {
            // Create an instance of the config object to return
            T configObj = Activator.CreateInstance<T>();

            // Get a list of the fields declared in the config object
            Type configObjType = typeof(T);

            // Create a dictionary to record which fields are found in the class files
            Dictionary<string, FieldInfo> classConfigField = new Dictionary<string, FieldInfo>();

            // This goes through each field of the class to set values
            // if found in the configuration file
            foreach (FieldInfo field in configObjType.GetFields())
            {
                Type fieldType = field.FieldType;

                // If thie field is an object, loop through its fields ("subfields")
                if (Type.GetTypeCode(fieldType) == TypeCode.Object)
                {

                    // Get the sub object value from the config object
                    object configObjSubObj = field.GetValue(configObj);

                    foreach (FieldInfo subField in fieldType.GetFields())
                    {

                        // If the INI file has a section/key pair corresponding to the field/subfield,
                        // set the subfield value and store the field info in dictionary
                        if (_iniData.Sections.ContainsSection(field.Name) && _iniData[field.Name].ContainsKey(subField.Name))
                        {
                            SetFieldValue(subField, configObjSubObj, _iniData[field.Name][subField.Name]);
                            string fieldName = field.Name + "." + subField.Name;
                            classConfigField[fieldName.ToUpper()] = subField;
                        }
                        else
                        {
                            Logger.config.Debug($"{field.Name}.{subField.Name} doesn't have a configuration value! Keeping existing value {subField.GetValue(configObjSubObj)}");
                        }
                    }
                }
                else
                {
                    // If a field in the configuration object isn't itself an object, then it's a primitive type
                    // declared in the global section of the INI file
                    if (_iniData.Global.ContainsKey(field.Name))
                    {
                        SetFieldValue(field, configObj, _iniData.Global[field.Name]);
                    }
                    else
                    {
                        Logger.config.Debug($"{field.Name} doesn't have a configuration value! Keeping existing value {field.GetValue(configObj)}");
                    }
                    string fieldName = field.Name;

                    // store field info in dictionary (case insensitive)
                    classConfigField[fieldName.ToUpper()] = field;
                }
            }

            // Loop through the global section of the INI file and see if any of those keys
            // don't correspond to a field in the object class by using dictionary

            // If any of them don't correspond to a field in the object class, add a comment to INI file
            // mentioning those keys are being ignored
            foreach (KeyData globalKey in _iniData.Global)
            {
                string fieldName = globalKey.KeyName;
                if (!classConfigField.ContainsKey(fieldName.ToUpper()))
                {
                    string missingClassFieldComment = "***THE FOLLOWING VALUE IS BEING IGNORED!" + configObj.GetType() + " does not have a field corresponding to " + globalKey.KeyName;
                    if(!globalKey.Comments.Contains(missingClassFieldComment))
                        globalKey.Comments.Add(missingClassFieldComment);
                    Logger.config.Debug($"{configObj.GetType()} does not have global section key {globalKey.KeyName}");
                }
            }

            // Similarly, loop through the other section/key pairings of the INI file and check as well.
            foreach (SectionData section in _iniData.Sections)
            {
                foreach (KeyData key in section.Keys)
                {
                    string fieldName = section.SectionName + "." + key.KeyName;
                    if (!classConfigField.ContainsKey(fieldName.ToUpper()))
                    {
                        string missingClassFieldComment = "***THE FOLLOWING VALUE IS BEING IGNORED! " + configObj.GetType() + " does not have a member corresponding to " + fieldName;
                        if (!key.Comments.Contains(missingClassFieldComment))
                            key.Comments.Add(missingClassFieldComment);
                        Logger.config.Debug($"{configObj.GetType()} not have {section.SectionName} section key {key.KeyName}");
                    }
                }
            }

            return configObj;
        }
        internal static void SetFieldValue(FieldInfo fieldInfo, object obj, string str)
        {
            if (str == null)
            {
                Logger.config.Debug($"{fieldInfo.Name} doesn't have a configuration value! Keeping existing value {fieldInfo.GetValue(obj)}");
                return;
            }

            switch (Type.GetTypeCode(fieldInfo.FieldType))
            {
                case TypeCode.String:
                    fieldInfo.SetValue(obj, str);
                    break;
                case TypeCode.Boolean:
                    fieldInfo.SetValue(obj, Boolean.Parse(str));
                    break;
                case TypeCode.DateTime:
                    fieldInfo.SetValue(obj, DateTime.Parse(str));
                    break;
                case TypeCode.Int16:
                    fieldInfo.SetValue(obj, Int16.Parse(str));
                    break;
                case TypeCode.Int32:
                    fieldInfo.SetValue(obj, Int32.Parse(str));
                    break;
                case TypeCode.Int64:
                    fieldInfo.SetValue(obj, Int64.Parse(str));
                    break;
                case TypeCode.Double:
                    fieldInfo.SetValue(obj, Double.Parse(str));
                    break;
                default:
                    Logger.config.Debug($"{fieldInfo.FieldType} not supported");
                    throw new Exception();
            }
        }

        public void Save()
        {
            Logger.config.Debug($"Saving file {Filename}");
            if (!Directory.Exists(Path.GetDirectoryName(Filename)))
                Directory.CreateDirectory(Path.GetDirectoryName(Filename) ?? throw new InvalidOperationException());

            var parser = new FileIniDataParser();
            parser.WriteFile(Filename, _iniData);

            HasChanged = false;
        }


        // This is basically serializing from an object to INI Data
        public void Store<T>(T obj)
        {
            Type configObjType = typeof(T);

            // Loop through each field in the config object and set the corresponding 
            // value in the INI Data object.

            // Note if there isn't a corresponding value defined in the INI data object,
            // it will add one implicitly by accessing it with the brackets
            foreach (FieldInfo field in configObjType.GetFields())
            {
                Type fieldType = field.FieldType;

                // if the field is not a primitive type, loop through its subfields
                if (Type.GetTypeCode(fieldType) == TypeCode.Object)
                {
                    FieldInfo[] subFields = fieldType.GetFields();
                    foreach (FieldInfo subField in subFields)
                    {
                        if (Type.GetTypeCode(subField.FieldType) != TypeCode.Object)
                            _iniData[field.Name][subField.Name] = subField.GetValue(field.GetValue(obj)).ToString();
                    }
                }
                else
                {
                    _iniData.Global[field.Name] = field.GetValue(obj).ToString();
                }
            }

            HasChanged = true;
            InMemoryChanged = true;
        }
        public string ReadValue(string section, string key)
        {
            return _iniData[section][key];
        }
        public string ReadValue(string key)
        {
            return _iniData.Global[key];
        }
    }
}