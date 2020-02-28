using System;
using System.Diagnostics;
using UnityEngine;
using Logger = IPA.Logging.Logger;

namespace IPA.Injector
{
    internal class Bootstrapper : MonoBehaviour
    {
        public event Action Destroyed = delegate {};

        public void Awake()
        {
        }

        public void Start()
        {
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            Destroyed();
        }
    }
}
