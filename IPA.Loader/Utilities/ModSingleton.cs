using UnityEngine;

namespace IPA.Utilities
{
    /// <summary>
    /// Generate a persistent singleton which can be destroyed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ModSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// The stored reference for the instance
        /// </summary>
        protected static T _instance;
        /// <summary>
        /// The lock for the instance to prevent more than one being created.
        /// </summary>
        protected static object _lock = new object();

        /// <summary>
        /// Checks to see if the singleton if the singleton can be accessed
        /// </summary>
        public static bool IsSingletonAvailable => _instance != null;

        /// <summary>
        /// Noncapitalized version which points to the actual property.
        /// </summary>
        public static T instance => Instance;

        /// <summary>
        /// Creates and or returns the singleton
        /// </summary>
        public static T Instance
        {
            get
            {
                T result;
                object @lock = _lock;
                lock (@lock)
                {
                    if (_instance == null)
                    {
                        _instance = (T)((object)FindObjectOfType(typeof(T)));
                        if (FindObjectsOfType(typeof(T)).Length > 1)
                        {
                            IPA.Logging.Logger.log.Warn($"[Singleton] Something went really wrong - there should never be more than one singleton of {nameof(T)}");
                            return _instance;
                        }
                        if (_instance == null)
                        {
                            GameObject gameObject = new GameObject();
                            _instance = gameObject.AddComponent<T>();
                            gameObject.name = $"{nameof(T)} Singleton";
                            DontDestroyOnLoad(gameObject);
                        }
                    }
                    result = _instance;
                }
                return result;
            }
        }

        /// <summary>
        /// Called when the singleton is enabled to prevent the singleton from being destroyed naturally
        /// </summary>
        private void OnEnabled()
        {
            DontDestroyOnLoad(this);
        }

        /// <summary>
        /// Destroys the singleton.
        /// </summary>
        public static void Destroy()
        {
            if (_instance != null)
            {
                _lock = new object();
                Destroy(_instance);
            }
        }

        /// <summary>
        /// Touches the instance to easily create it without having to assign it.
        /// </summary>
        public static void TouchInstance()
        {
            _ = Instance == null;
        }
    }
}
