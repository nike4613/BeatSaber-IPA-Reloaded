using System;
using System.Diagnostics;
using UnityEngine;
using Logger = IPA.Logging.Logger;

namespace IPA.Injector
{
    internal class Bootstrapper : MonoBehaviour
    {
        public event Action Destroyed = delegate {};

        public void Start()
        public void A
        {
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            Destroyed();
        }
    }
}
