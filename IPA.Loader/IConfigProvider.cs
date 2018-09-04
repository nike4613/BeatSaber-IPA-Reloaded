using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA
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
        /// <remarks>
        /// NOTE TO IMPLEMENTERS:
        /// Since <see cref="Newtonsoft.Json"/> is bundled with this library, try to include support for its attributes.
        /// </remarks>
        /// <typeparam name="T">the type of <paramref name="obj"/></typeparam>
        /// <param name="obj">the object containing the data to save</param>
        void Store<T>(T obj);

        #region Getters
        /// <summary>
        /// Gets the <see cref="IConfigProvider"/> acting as a sub-object for a given key.
        /// </summary>
        /// <param name="name">the name of the field with the <see cref="IConfigProvider"/></param>
        /// <returns>an accessor for the selected sub-object</returns>
        IConfigProvider GetSubObject(string name);
        /// <summary>
        /// Gets the value of type <typeparamref name="T"/> for key <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> is <see cref="IConfigProvider"/>, behavior should be identical to <see cref="GetSubObject"/>.
        /// </remarks>
        /// <typeparam name="T">the type of the value to get</typeparam>
        /// <param name="name">the name of the field to get the value of</param>
        /// <returns>the value of the field</returns>
        T Get<T>(string name); // can be IConfigProvider
        /// <summary>
        /// The non-generic version of <see cref="Get{T}"/>.
        /// If key corresponds to a sub-object, will return an <see cref="IConfigProvider"/>.
        /// </summary>
        /// <param name="name">the name of the field to get the value of</param>
        /// <returns>the value of the field</returns>
        object Get(string name); // can return IConfigProvider
        /// <summary>
        /// Gets the value of type <typeparamref name="T"/> of the element at <paramref name="path"/>.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> is <see cref="IConfigProvider"/>, behavior should be identical to <see cref="GetSubObject"/> if it were executed on the immediate parent of the target element.
        /// </remarks>
        /// <typeparam name="T">the type of the value to get</typeparam>
        /// <param name="path">an ordered array specifying keys starting from the root (this)</param>
        /// <returns>the value at path</returns>
        T GetPath<T>(params string[] path);
        /// <summary>
        /// The non-generic version of <see cref="GetPath{T}(string[])"/>.
        /// If key corresponds to a sub-object, will return an <see cref="IConfigProvider"/>.
        /// </summary>
        /// <param name="path">an ordered array specifying keys starting from the root (this)</param>
        /// <returns>the value at path</returns>
        object GetPath(params string[] path);
        #endregion

        #region Setters
        /// <summary>
        /// Sets the object for key '<paramref name="name"/>' to <paramref name="provider"/>.
        /// </summary>
        /// <param name="name">the key to set it as</param>
        /// <param name="provider">the provider value</param>
        void SetSubObject(string name, IConfigProvider provider); // argument should be same provider type
        /// <summary>
        /// Sets the value for key <paramref name="name"/> to <paramref name="value"/>.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> is <see cref="IConfigProvider"/>, behavior should be identical to <see cref="SetSubObject(string, IConfigProvider)"/>.
        /// </remarks>
        /// <typeparam name="T">the type of the value to set</typeparam>
        /// <param name="name">the key</param>
        /// <param name="value">the value to set</param>
        void Set<T>(string name, T value);
        /// <summary>
        /// Sets the value for path <paramref name="path"/> to <paramref name="value"/>.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> is <see cref="IConfigProvider"/>, behavior should be identical to <see cref="SetSubObject(string, IConfigProvider)"/> if it were executed on the immediate parent of the target element.
        /// </remarks>
        /// <typeparam name="T">the type of the value to set</typeparam>
        /// <param name="path">the path to the new value location</param>
        /// <param name="value">the value to set</param>
        void SetPath<T>(string[] path, T value);
        #endregion
    }
}
