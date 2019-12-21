using System;
using System.IO;
using System.Threading.Tasks;
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
        /// Stores the <see cref="Value"/> given to disk in the format specified.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to store</param>
        /// <param name="file">the file to write to</param>
        void Store(Value value, FileInfo file);

        /// <summary>
        /// Loads a <see cref="Value"/> from disk in whatever format this provider provides
        /// and returns it.
        /// </summary>
        /// <param name="file">the file to read from</param>
        /// <returns>the <see cref="Value"/> loaded</returns>
        Value Load(FileInfo file);
    }

    /// <summary>
    /// A wrapper for an <see cref="IConfigProvider"/> and the <see cref="FileInfo"/> to use with it.
    /// </summary>
    public class ConfigProvider // this *should* be a struct imo, but mono doesn't seem to like that
    {
        private readonly FileInfo file;
        private readonly IConfigProvider provider;

        internal ConfigProvider(FileInfo file, IConfigProvider provider)
        {
            this.file = file; this.provider = provider;
        }

        /// <summary>
        /// Stores the <see cref="Value"/> given to disk in the format specified.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to store</param>
        public void Store(Value value) => provider.Store(value, file);
        /// <summary>
        /// Loads a <see cref="Value"/> from disk in whatever format this provider provides
        /// and returns it.
        /// </summary>
        /// <returns>the <see cref="Value"/> loaded</returns>
        public Value Load() => provider.Load(file);
    }
}
