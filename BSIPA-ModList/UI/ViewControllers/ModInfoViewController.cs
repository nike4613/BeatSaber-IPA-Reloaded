using CustomUI.BeatSaber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRUI;

namespace BSIPA_ModList.UI
{
    internal class ModInfoViewController : VRUIViewController
    {
        private Sprite Icon;
        private string Name;
        private string Version;
        private string Author;
        private string Description;
        private bool CanUpdate;

        public void Init(Sprite icon, string name, string version, string author, string description, bool canUpdate)
        {
            Icon = icon;
            Name = name;
            Version = version;
            Author = author;
            Description = description;
            CanUpdate = canUpdate;
        }
    }
}
