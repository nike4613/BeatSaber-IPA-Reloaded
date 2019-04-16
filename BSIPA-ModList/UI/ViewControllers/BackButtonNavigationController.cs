using CustomUI.BeatSaber;
using System;
using UnityEngine.UI;
using VRUI;

namespace BSIPA_ModList.UI
{
    internal class BackButtonNavigationController : VRUINavigationController
    {
        public event Action didFinishEvent;

        private Button _backButton;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform, didFinishEvent.Invoke);
            }
        }
    }
}