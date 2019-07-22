using System;
// ReSharper disable UnusedMember.Global

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

#if NET4
        /// <summary>
        /// Gets a dynamic object providing access to the configuration.
        /// </summary>
        /// <value>a dynamically bound object to use to access config values directly</value>
        dynamic Dynamic { get; }
#endif

#region State getters
        /// <summary>
        /// Returns <see langword="true"/> if object has changed since the last save
        /// </summary>
        /// <value><see langword="true"/> if object has changed since the last save, else <see langword="false"/></value>
        bool HasChanged { get; }
        /// <summary>
        /// Returns <see langword="true"/> if the data in memory has been changed - notably including loads.
        /// </summary>
        /// <value><see langword="true"/> if the data in memory has been changed, else <see langword="false"/></value>
        bool InMemoryChanged { get; set; }
        /// <summary>
        /// Will be set with the filename (no extension) to save to. When saving, the implementation should add the appropriate extension. Should error if set multiple times.
        /// </summary>
        /// <value>the extensionless filename to save to</value>
        string Filename { set; }
        /// <summary>
        /// Gets the last time the config was modified.
        /// </summary>
        /// <value>the last time the config file was modified</value>
        DateTime LastModified { get; }
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
