using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config
{
    /// <summary>
    /// An interface for configuration providers.
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>
        /// Loads the data provided by this <see cref="IConfigProvider"/> into an object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">the type of the object to parse into</typeparam>
        /// <returns>the values from the config provider parsed into the object</returns>
        T Parse<T>();
        /// <summary>
        /// Stores the data from <paramref name="obj"/> into the <see cref="IConfigProvider"/>.
        /// </summary>
        /// <typeparam name="T">the type of <paramref name="obj"/></typeparam>
        /// <param name="obj">the object containing the data to save</param>
        void Store<T>(T obj);

        /// <summary>
        /// Gets a dynamic object providing access to the configuration.
        /// </summary>
        dynamic Dynamic { get; }

        #region State getters
        /// <summary>
        /// Returns <see langword="true"/> if object has changed since the last save
        /// </summary>
        bool HasChanged { get; }
        /// <summary>
        /// Will be set with the filename (no extension) to save to. When saving, the implimentation should add the appropriate extension. Should error if set multiple times.
        /// </summary>
        string Filename { set; }
        /// <summary>
        /// Saves configuration to file. Should error if not a root object.
        /// </summary>
        void Save();
        /// <summary>
        /// Loads the state of the file on disk.
        /// </summary>
        void Load();
        #endregion
    }
}
