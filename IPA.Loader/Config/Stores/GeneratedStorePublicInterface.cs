using IPA.Config.Stores.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace IPA.Config.Stores
{
    /// <summary>
    /// A class providing an extension for <see cref="Config"/> to make it easy to use generated
    /// config stores.
    /// </summary>
    public static class GeneratedStore
    {
        /// <summary>
        /// The name of the assembly that internals must be visible to to allow internal protection.
        /// </summary>
        public const string AssemblyVisibilityTarget = GeneratedStoreImpl.GeneratedAssemblyName;

        /// <summary>
        /// Creates a generated <see cref="IConfigStore"/> of type <typeparamref name="T"/>, registers it to
        /// the <see cref="Config"/> object, and returns it. This also forces a synchronous config load via
        /// <see cref="Config.LoadSync"/> if <paramref name="loadSync"/> is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <typeparamref name="T"/> must be a public non-<see langword="sealed"/> class.
        /// It can also be internal, but in that case, then your assembly must have the following attribute
        /// to allow the generated code to reference it.
        /// <code lang="csharp">
        /// [assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.AssemblyVisibilityTarget)]
        /// </code>
        /// </para>
        /// <para>
        /// Only fields and properties that are public or protected will be considered, and only properties
        /// where both the getter and setter are public or protected are considered. Any fields or properties
        /// with an <see cref="IgnoreAttribute"/> applied to them are also ignored. Having properties be <see langword="virtual"/> is not strictly
        /// necessary, however it allows the generated type to keep track of changes and lock around them so that the config will auto-save.
        /// </para>
        /// <para>
        /// All of the attributes in the <see cref="Attributes"/> namespace are handled as described by them.
        /// </para>
        /// <para>
        /// If the <typeparamref name="T"/> declares a public or protected, <see langword="virtual"/>
        /// method <c>Changed()</c>, then that method may be called to artificially signal to the runtime that the content of the object 
        /// has changed. That method will also be called after the write locks are released when a property is set anywhere in the owning
        /// tree. This will only be called on the outermost generated object of the config structure, even if the change being signaled
        /// is somewhere deep into the tree.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a public or protected, <see langword="virtual"/> 
        /// method <c>OnReload()</c>, which will be called on the filesystem reader thread after the object has been repopulated with new data 
        /// values. It will be called <i>after</i> the write lock for this object is released. This will only be called on the outermost generated
        /// object of the config structure.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a public or protected, <see langword="virtual"/> 
        /// method <c>CopyFrom(ConfigType)</c> (the first parameter is the type it is defined on), which may be called to copy the properties from
        /// another object of its type easily, and more importantly, as only one change. Its body will be executed after the values have been copied.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a public or protected, <see langword="virtual"/> 
        /// method <c>ChangeTransaction()</c> returning <see cref="IDisposable"/>, which may be called to get an object representing a transactional
        /// change. This may be used to change a lot of properties at once without triggering a save multiple times. Ideally, this is used in a
        /// <see langword="using"/> block or declaration. The <see cref="IDisposable"/> returned from your implementation will have its
        /// <see cref="IDisposable.Dispose"/> called <i>after</i> <c>Changed()</c> is called, but <i>before</i> the write lock is released.
        /// Unless you have a very good reason to use the nested <see cref="IDisposable"/>, avoid it.
        /// </para>
        /// <para>
        /// If <typeparamref name="T"/> is marked with <see cref="NotifyPropertyChangesAttribute"/>, the resulting object will implement
        /// <see cref="INotifyPropertyChanged"/>. Similarly, if <typeparamref name="T"/> implements <see cref="INotifyPropertyChanged"/>,
        /// the resulting object will implement it and notify it too.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the type to wrap</typeparam>
        /// <param name="cfg">the <see cref="Config"/> to register to</param>
        /// <param name="loadSync">whether to synchronously load the content, or trigger an async load</param>
        /// <returns>a generated instance of <typeparamref name="T"/> as a special <see cref="IConfigStore"/></returns>
        public static T Generated<T>(this Config cfg, bool loadSync = true) where T : class
        {
            var ret = GeneratedStoreImpl.Create<T>();
            cfg.SetStore(ret as IConfigStore);
            if (loadSync)
                cfg.LoadSync();
            else
                cfg.LoadAsync();

            return ret;
        }

        /// <summary>
        /// Creates a generated store outside of the context of the config system.
        /// </summary>
        /// <remarks>
        /// See <see cref="Generated{T}(Config, bool)"/> for more information about how it behaves.
        /// </remarks>
        /// <typeparam name="T">the type to wrap</typeparam>
        /// <returns>a generated instance of <typeparamref name="T"/> implementing functionality described by <see cref="Generated{T}(Config, bool)"/></returns>
        /// <seealso cref="Generated{T}(Config, bool)"/>
        public static T Create<T>() where T : class
            => GeneratedStoreImpl.Create<T>();
    }
}