using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionPlugin
{
    public interface IGenericEnhancedPlugin
    {
        /// <summary>
        /// Gets a list of executables this plugin should be excuted on (without the file ending)
        /// </summary>
        /// <example>{ "PlayClub", "PlayClubStudio" }</example>
        string[] Filter { get; }

        void OnLateUpdate();
    }
}
