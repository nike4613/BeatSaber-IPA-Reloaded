using System;
using Version = SemVer.Version;

// ReSharper disable CheckNamespace

namespace IPA
{
    /// <summary>
    /// A class to provide information about a mod on ModSaber.ML
    /// </summary>
    // ReSharper disable once IdentifierTypo
    public class ModsaberModInfo
    {
        /// <summary>
        /// The name the mod uses on ModSaber as an identifier.
        /// </summary>
        public string InternalName
        {
            get => _internalName;
            set
            {
                if (_internalName == null)
                {
                    _internalName = value;
                }
                else
                {
                    throw new Exception("Cannot change name one it has been set!");
                }
            }
        }
        private string _internalName;

        /// <summary>
        /// The version of the currently installed mod. Used to compare to the version on ModSaber. Should be a valid SemVer version.
        /// </summary>
        public string CurrentVersion
        {
            get => _currentVersion;
            set
            {
                if (_currentVersion == null)
                {
                    var version = new Version(value); // check for valid version
                    _currentVersion = value;
                    SemverVersion = version;
                }
                else
                {
                    throw new Exception("Cannot change version one it has been set!");
                }
            }
        }
        private string _currentVersion;

        internal Version SemverVersion;
    }
}
