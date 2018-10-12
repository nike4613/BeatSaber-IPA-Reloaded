using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA
{
    /// <summary>
    /// A class to provide information about a mod on ModSaber.ML
    /// </summary>
    public class ModsaberModInfo
    {
        /// <summary>
        /// The name the mod uses on ModSaber as an identifier.
        /// </summary>
        public string InternalName
        {
            get => _InternalName;
            set
            {
                if (_InternalName == null)
                {
                    _InternalName = value;
                }
                else
                {
                    throw new Exception("Cannot change name one it has been set!");
                }
            }
        }
        private string _InternalName = null;

        /// <summary>
        /// The version of the currently installed mod. Used to compare to the version on ModSaber. Should be a valid SemVer version.
        /// </summary>
        public string CurrentVersion
        {
            get => _CurrentVersion;
            set
            {
                if (_CurrentVersion == null)
                {
                    _CurrentVersion = value;
                }
                else
                {
                    throw new Exception("Cannot change version one it has been set!");
                }
            }
        }
        private string _CurrentVersion = null;
    }
}
