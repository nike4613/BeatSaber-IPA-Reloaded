using System;
using UnityEngine;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

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
