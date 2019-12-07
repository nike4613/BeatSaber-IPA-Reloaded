using System;
using System.IO;
using IPA.Config.Data;

namespace IPA.Config
{
    /// <summary>
    /// An interface for configuration providers.
    /// </summary>
    /// <remarks>
    /// Implementers must provide a default constructor. Do not assume that <see cref="File"/> will ever be set for a given object.
    /// </remarks>
    public interface IConfigProvider
    {
        /// <summary>
        /// Gets the extension <i>without</i> a dot to use for files handled by this provider.
        /// </summary>
        /// <remarks>
        /// This must work immediately, and is used to generate the <see cref="FileInfo"/> used to set
        /// <see cref="File"/>.
        /// </remarks>
        string Extension { get; }

        /// <summary>
        /// Sets the file that this provider will read and write to.
        /// </summary>
        /// <remarks>
        /// The provider is expected to gracefully handle this changing at any point, 
        /// and is expected to close any old file handles when this is reassigned.
        /// This may be set to the same file multiple times in this object's lifetime.
        /// This will always have been set at least once before any calls to <see cref="Load"/>
        /// or <see cref="Store"/> are made.
        /// </remarks>
        FileInfo File { set; }

        /// <summary>
        /// Stores the <see cref="Value"/> given to disk in the format specified.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to store</param>
        void Store(Value value);

        /// <summary>
        /// Loads a <see cref="Value"/> from disk in whatever format this provider provides
        /// and returns it.
        /// </summary>
        /// <returns>the <see cref="Value"/> loaded</returns>
        Value Load();
    }
}
