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
