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
        public string InternalName { get; set; }
        
        /// <summary>
        /// The version of the currently installed mod. Used to compare to the version on ModSaber.
        /// </summary>
        public Version CurrentVersion { get; set; }
    }
}
