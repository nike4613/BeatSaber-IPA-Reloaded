using System;
using IPA;
using IPA.Logging;
using IPA.Config;
using IPA.Config.Stores;

namespace Demo
{
    /*
    [Plugin(RuntimeOptions.DynamicInit)]
    */
    [Plugin(RuntimeOptions.SingleStartInit)]
    internal class Plugin
    {
        public static Logger log { get; private set; }

        [Init]
        public Plugin(Logger logger, Config conf)
        {
            log = logger;
            PluginConfig.Instance = conf.Generated<PluginConfig>();
            log.Debug("Config loaded");

            // setup that does not require game code
            // this is only called once ever, so do once-ever initialization
        }

        /*
        [Init]
        public Plugin(Logger logger)
        {
            log = logger;
            log.Debug("Basic plugin running!");

            // setup that does not require game code
            // this is only called once ever, so do once-ever initialization
        }
        */

        [Init]
        public void Init(Logger logger)
        {
            // logger will be the same instance as log currently is
        }

        /*
        [OnEnable]
        public void OnEnable()
        */
        [OnStart]
        public void OnStart()
        {
            // setup that requires game code
        }

        /*
        [OnDisable]
        public void OnDisable()
        */
        [OnExit]
        public void OnExit()
        {
            // teardown
            // this may be called mid-game if you are using RuntimeOptions.DynamicInit
        }
    }
}