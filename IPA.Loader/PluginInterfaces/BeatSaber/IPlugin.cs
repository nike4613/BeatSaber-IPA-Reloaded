using UnityEngine.SceneManagement;

namespace IPA
{
    /// <summary>
    /// Interface for BSIPA plugins. Every class that implements this will be loaded if the DLL is placed at
    /// &lt;install dir&gt;/Plugins.
    /// </summary>
    /// <remarks>
    /// Mods implemented with this interface should handle being enabled at runtime properly, unless marked
    /// with the "no-runtime-enable" feature.
    /// </remarks>
    public interface IPlugin
    {
        /// <summary>
        /// Called when a plugin is enabled. This is where you should set up Harmony patches and the like.
        /// </summary>
        /// <remarks>
        /// This will be called after Init, and will be called when the plugin loads normally too.
        /// When a plugin is disabled at startup, neither this nor Init will be called until it is enabled.
        /// 
        /// Init will only ever be called once.
        /// </remarks>
        void OnEnable();

        /// <summary>
        /// Gets invoked when the application is closed.
        /// </summary>
        void OnApplicationQuit();

        /// <summary>
        /// Gets invoked on every graphic update.
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// Gets invoked on ever physics update.
        /// </summary>
        void OnFixedUpdate();

        /// <summary>
        /// Gets invoked whenever a scene is loaded.
        /// </summary>
        /// <param name="scene">The scene currently loaded</param>
        /// <param name="sceneMode">The type of loading</param>
        void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode);

        /// <summary>
        /// Gets invoked whenever a scene is unloaded
        /// </summary>
        /// <param name="scene">The unloaded scene</param>
        void OnSceneUnloaded(Scene scene);

        /// <summary>
        /// Gets invoked whenever a scene is changed
        /// </summary>
        /// <param name="prevScene">The Scene that was previously loaded</param>
        /// <param name="nextScene">The Scene being loaded</param>
        void OnActiveSceneChanged(Scene prevScene, Scene nextScene);
    }
}
