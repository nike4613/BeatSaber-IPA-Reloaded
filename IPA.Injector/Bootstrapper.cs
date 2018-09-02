using IllusionInjector.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IPA.Injector
{
    class Bootstrapper : MonoBehaviour
    {
        public event Action Destroyed = delegate {};
        
        void Awake()
        {
            //if (Environment.CommandLine.Contains("--verbose"))
            //{
            Ipa.Injector.Windows.GuiConsole.CreateConsole();
            //}
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
