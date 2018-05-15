using UnityEngine.SceneManagement;

namespace IllusionPlugin {
    public interface IPluginNew : IPlugin{
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