using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using IPA.Loader;
using IPA.Loader.Composite;
using IPA.Logging;

namespace IPA.Loader
{
    internal class PluginComponent : MonoBehaviour
    {
        private CompositeBSPlugin bsPlugins;
        private CompositeIPAPlugin ipaPlugins;
        private bool quitting = false;

        internal static PluginComponent Create()
        {
            return new GameObject("IPA_PluginManager").AddComponent<PluginComponent>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            bsPlugins = new CompositeBSPlugin(PluginManager.BSPlugins);
            ipaPlugins = new CompositeIPAPlugin(PluginManager.Plugins);
            
            gameObject.AddComponent<Updating.ModsaberML.Updater>();

            bsPlugins.OnApplicationStart();
            ipaPlugins.OnApplicationStart();
            
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            foreach (var provider in PluginManager.configProviders)
                if (provider.Key.HasChanged)
                    try
                    {
                        provider.Key.Save();
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.log.Error("Error when trying to save config");
                        Logging.Logger.log.Error(e);
                    }
        }

        void Update()
        {
            bsPlugins.OnUpdate();
            ipaPlugins.OnUpdate();
        }

        void LateUpdate()
        {
            bsPlugins.OnLateUpdate();
            ipaPlugins.OnLateUpdate();

            foreach (var provider in PluginManager.configProviders)
            {
                if (provider.Key.HasChanged)
                    try
                    {
                        provider.Key.Save();
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.log.Error("Error when trying to save config");
                        Logging.Logger.log.Error(e);
                    }
                else if (provider.Key.LastModified > provider.Value.Value)
                {
                    try
                    {
                        provider.Key.Load(); // auto reload if it changes
                        provider.Value.Value = provider.Key.LastModified;
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.log.Error("Error when trying to load config");
                        Logging.Logger.log.Error(e);
                    }
                }
            }
        }

        void FixedUpdate()
        {
            bsPlugins.OnFixedUpdate();
            ipaPlugins.OnFixedUpdate();
        }

        void OnDestroy()
        {
            if (!quitting)
            {
                Create();
            }
        }
        
        void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            
            bsPlugins.OnApplicationQuit();
            ipaPlugins.OnApplicationQuit();

            foreach (var provider in PluginManager.configProviders)
                if (provider.Key.HasChanged)
                    try
                    {
                        provider.Key.Save();
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.log.Error("Error when trying to save config");
                        Logging.Logger.log.Error(e);
                    }

            quitting = true;
        }

        void OnLevelWasLoaded(int level)
        {
            ipaPlugins.OnLevelWasLoaded(level);
        }

        void OnLevelWasInitialized(int level)
        {
            ipaPlugins.OnLevelWasInitialized(level);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            bsPlugins.OnSceneLoaded(scene, sceneMode);
        }
        
        private void OnSceneUnloaded(Scene scene) {
            bsPlugins.OnSceneUnloaded(scene);
        }

        private void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
            bsPlugins.OnActiveSceneChanged(prevScene, nextScene);
        }

    }
}
