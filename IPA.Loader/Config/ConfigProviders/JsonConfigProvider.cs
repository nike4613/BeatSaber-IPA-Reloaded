using IPA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.ConfigProviders
{
    internal class JsonConfigProvider : IConfigProvider
    {
        private JObject jsonObj;

        // TODO: create a wrapper that allows empty object creation
        public dynamic Dynamic => jsonObj;

        public bool HasChanged { get; private set; } = false;

        public DateTime LastModified => File.GetLastWriteTime(Filename + ".json");

        private string _filename = null;
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

            var finfo = new FileInfo(Filename + ".json");
            if (finfo.Exists)
            {
                string json = finfo.OpenText().ReadToEnd();
                try
                {
                    jsonObj = JObject.Parse(json);
                }
                catch (Exception e)
                {
                    Logger.config.Error($"Error parsing JSON in file {Filename}.json; resetting to empty JSON");
                    Logger.config.Error(e);
                    jsonObj = new JObject();
                    File.WriteAllText(finfo.FullName, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
                }
            }
            else
            {
                jsonObj = new JObject();
            }

            SetupListeners();
        }

        private void SetupListeners()
        {
            jsonObj.PropertyChanged += JsonObj_PropertyChanged;
            jsonObj.ListChanged += JsonObj_ListChanged;
            jsonObj.CollectionChanged += JsonObj_CollectionChanged;
        }

        private void JsonObj_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HasChanged = true;
        }

        private void JsonObj_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
        {
            HasChanged = true;
        }

        private void JsonObj_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            HasChanged = true;
        }

        public T Parse<T>()
        {
            return jsonObj.ToObject<T>();
        }

        public void Save()
        {
            Logger.config.Debug($"Saving file {Filename}.json");

            var finfo = new FileInfo(Filename + ".json");

            File.WriteAllText(finfo.FullName, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));

            HasChanged = false;
        }

        public void Store<T>(T obj)
        {
            jsonObj = JObject.FromObject(obj);
            SetupListeners();
            HasChanged = true;
        }
    }
}
