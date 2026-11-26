#nullable enable
using System;
using UnityEngine;

namespace IPA.Injector
{
    internal class Bootstrapper : MonoBehaviour
    {
        public event Action Destroyed = null!;

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
