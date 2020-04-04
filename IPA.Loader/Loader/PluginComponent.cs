using IPA.Config;
using IPA.Loader.Composite;
using IPA.Utilities;
using IPA.Utilities.Async;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.SceneManagement;
// ReSharper disable UnusedMember.Local

namespace IPA.Loader
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal class PluginComponent : MonoBehaviour
    {
        private CompositeBSPlugin bsPlugins;
        private CompositeIPAPlugin ipaPlugins;
        private bool quitting;
        private static bool initialized = false;

        internal static PluginComponent Create()
        {
            return new GameObject("IPA_PluginManager").AddComponent<PluginComponent>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (!initialized)
            {
                UnityGame.SetMainThread();
                UnityGame.EnsureRuntimeGameVersion();

                PluginManager.Load();

                bsPlugins = new CompositeBSPlugin(PluginManager.BSMetas);
#pragma warning disable 618
                ipaPlugins = new CompositeIPAPlugin(PluginManager.Plugins);
#pragma warning restore 618

#if BeatSaber
                gameObject.AddComponent<Updating.BeatMods.Updater>();
#endif

                bsPlugins.OnEnable();
                ipaPlugins.OnApplicationStart();

                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;

                var unitySched = UnityMainThreadTaskScheduler.Default as UnityMainThreadTaskScheduler;
                if (!unitySched.IsRunning)
                    StartCoroutine(unitySched.Coroutine());

                initialized = true;

#if DEBUG
                Config.Stores.GeneratedStoreImpl.DebugSaveAssembly("GeneratedAssembly.dll");
#endif
            }
        }

        void Update()
        {
            bsPlugins.OnUpdate();
            ipaPlugins.OnUpdate();

            var unitySched = UnityMainThreadTaskScheduler.Default as UnityMainThreadTaskScheduler;
            if (!unitySched.IsRunning)
                StartCoroutine(unitySched.Coroutine());
        }

        void LateUpdate()
        {
            bsPlugins.OnLateUpdate();
            ipaPlugins.OnLateUpdate();
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

            ConfigRuntime.ShutdownRuntime(); // this seems to be needed

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
