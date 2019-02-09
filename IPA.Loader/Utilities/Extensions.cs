using System;

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
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Unwraps a <see cref="Nullable{T}"/> where T is <see cref="bool"/> such that if the value is null, it gives <see langword="false"/>.
        /// </summary>
        /// <param name="self">the bool? to unwrap</param>
        /// <returns>the unwrapped value, or <see langword="false"/> if it was <see langword="null"/></returns>
        public static bool Unwrap(this bool? self) => self != null && self.Value;
    }
}
