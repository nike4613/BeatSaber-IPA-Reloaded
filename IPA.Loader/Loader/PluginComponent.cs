using IPA.Config;
using IPA.Loader.Composite;
using IPA.Utilities;
using IPA.Utilities.Async;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
// ReSharper disable UnusedMember.Local

namespace IPA.Loader
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal class PluginComponent : MonoBehaviour
    {
        private CompositeBSPlugin bsPlugins;
        private bool quitting;
        public static PluginComponent Instance;
        private static bool initialized = false;

        internal static PluginComponent Create()
        {
            return Instance = new GameObject("IPA_PluginManager").AddComponent<PluginComponent>();
        }

        internal void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (!initialized)
            {
                UnityGame.SetMainThread();
                UnityGame.EnsureRuntimeGameVersion();

                PluginManager.Load();

                bsPlugins = new CompositeBSPlugin(PluginManager.BSMetas);

                /*
#if BeatSaber // TODO: remove this
                gameObject.AddComponent<Updating.BeatMods.Updater>();
#endif
                */

                bsPlugins.OnEnable();

                var unitySched = UnityMainThreadTaskScheduler.Default as UnityMainThreadTaskScheduler;
                if (!unitySched.IsRunning)
                    StartCoroutine(unitySched.Coroutine());

                initialized = true;

#if DEBUG
                Config.Stores.GeneratedStoreImpl.DebugSaveAssembly($"{Config.Stores.GeneratedStoreImpl.GeneratedAssemblyName}.dll");
#endif
            }
        }

        internal void Update()
        {
            var unitySched = UnityMainThreadTaskScheduler.Default as UnityMainThreadTaskScheduler;
            if (!unitySched.IsRunning)
                StartCoroutine(unitySched.Coroutine());
        }

        internal void OnDestroy()
        {
            if (!quitting)
            {
                Create();
            }
        }

        internal void OnApplicationQuit()
        {
            bsPlugins.OnDisable();

            ConfigRuntime.ShutdownRuntime(); // this seems to be needed

            quitting = true;
        }
    }
}
