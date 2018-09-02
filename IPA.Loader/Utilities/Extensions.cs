using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Utilities
{
    /// <summary>
    /// A class providing various extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets the default value for a given <see cref="Type"/>.
        /// </summary>
        /// <param name="type">the <see cref="Type"/> to get the default value for</param>
        /// <returns>the default value of <paramref name="type"/></returns>
        public static object GetDefault(this Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }
    }
}
