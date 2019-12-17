using System;
using System.IO;
using IPA.Config.Data;

namespace IPA.Config
{
    /// <summary>
    /// An interface for configuration providers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementers must provide a default constructor. Do not assume that <see cref="File"/> will ever be set for a given object.
    /// </para>
    /// <para>
    /// Implementers are expected to preserve the typing of values passed to <see cref="Store"/> when returned from <see cref="Load"/>.
    /// The only exceptions to this are the numeric types, <see cref="Integer"/> and <see cref="FloatingPoint"/>, since they can be coerced
    /// to each other with <see cref="Integer.AsFloat"/> and <see cref="FloatingPoint.AsInteger"/> respectively. The provider <i>should</i>
    /// however store and recover <see cref="Integer"/> with as much precision as is possible. For example, a JSON provider may decide to
    /// decode all numbers that have an integral value, even if they were originally <see cref="FloatingPoint"/>, as <see cref="Integer"/>.
    /// This is reasonable, as <see cref="Integer"/> is more precise, particularly with larger values, than <see cref="FloatingPoint"/>.
    /// </para>
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

        // TODO: consider moving to asynchronous Store and Load with a FileInfo parameter

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
