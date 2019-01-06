using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using IPA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IPA.Config.ConfigProviders
{
    [Config.Type("json")]
    internal class JsonConfigProvider : IConfigProvider
    {
        public static void RegisterConfig()
        {
            Config.Register<JsonConfigProvider>();
        }

        private JObject jsonObj;

        // TODO: create a wrapper that allows empty object creation
        public dynamic Dynamic => jsonObj;
        
        public bool HasChanged { get; private set; }
        public bool InMemoryChanged { get; set; }

        public DateTime LastModified => File.GetLastWriteTime(Filename + ".json");

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

        public void Load()
        {
            Logger.config.Debug($"Loading file {Filename}.json");

            var fileInfo = new FileInfo(Filename + ".json");
            if (fileInfo.Exists)
            {
                string json = File.ReadAllText(fileInfo.FullName);
                try
                {
                    jsonObj = JObject.Parse(json);
                }
                catch (Exception e)
                {
                    Logger.config.Error($"Error parsing JSON in file {Filename}.json; resetting to empty JSON");
                    Logger.config.Error(e);
                    jsonObj = new JObject();
                    File.WriteAllText(fileInfo.FullName, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
                }
            }
            else
            {
                jsonObj = new JObject();
            }

            SetupListeners();
            InMemoryChanged = true;
        }

        private void SetupListeners()
        {
            jsonObj.PropertyChanged += JsonObj_PropertyChanged;
            jsonObj.ListChanged += JsonObj_ListChanged;
            jsonObj.CollectionChanged += JsonObj_CollectionChanged;
        }

        private void JsonObj_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            HasChanged = true;
            InMemoryChanged = true;
        }

        private void JsonObj_ListChanged(object sender, ListChangedEventArgs e)
        {
            HasChanged = true;
            InMemoryChanged = true;
        }

        private void JsonObj_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HasChanged = true;
            InMemoryChanged = true;
        }

        public T Parse<T>()
        {
            if (jsonObj == null)
                return default(T);
            return jsonObj.ToObject<T>();
        }

        public void Save()
        {
            Logger.config.Debug($"Saving file {Filename}.json");
            File.WriteAllText(Filename + ".json", JsonConvert.SerializeObject(jsonObj, Formatting.Indented));

            HasChanged = false;
        }

        public void Store<T>(T obj)
        {
            jsonObj = JObject.FromObject(obj);
            SetupListeners();
            HasChanged = true;
            InMemoryChanged = true;
        }
    }
}
