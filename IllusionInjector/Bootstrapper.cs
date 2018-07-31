using IllusionInjector.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IllusionInjector
{
    class Bootstrapper : MonoBehaviour
    {
        public event Action Destroyed = delegate {};
        
        void Awake()
        {
            //if (Environment.CommandLine.Contains("--verbose"))
            //{
                Windows.GuiConsole.CreateConsole();
            //}

            Application.logMessageReceived += delegate (string condition, string stackTrace, LogType type)
            {
                var level = UnityLogInterceptor.LogTypeToLevel(type);
                UnityLogInterceptor.Unitylogger.Log(level, $"{condition.Trim()}");
                UnityLogInterceptor.Unitylogger.Log(level, $"{stackTrace.Trim()}");
            };
        }

        void Start()
        {
            Destroy(gameObject);
        }
        void OnDestroy()
        {
            Destroyed();
        }
    }
}
