using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IPA.Config.Data;

namespace IPA.Config.Stores.Converters
{
    /// <summary>
    /// A base class for all <see cref="ICollection{T}"/> type converters, providing most of the functionality.
    /// </summary>
    /// <typeparam name="T">the type of the items in the collection</typeparam>
    /// <typeparam name="TCollection">the instantiated type of collection</typeparam>
    public class CollectionConverter<T, TCollection> : ValueConverter<TCollection>
        where TCollection : ICollection<T>
    {
        /// <summary>
        /// Creates a <see cref="CollectionConverter{T, TCollection}"/> using the default converter for the
        /// element type. Equivalent to calling <see cref="CollectionConverter{T, TCollection}.CollectionConverter(ValueConverter{T})"/>
        /// with <see cref="Converter{T}.Default"/>.
        /// </summary>
        /// <seealso cref="CollectionConverter{T, TCollection}.CollectionConverter(ValueConverter{T})"/>
        public CollectionConverter() : this(Converter<T>.Default) { }
        /// <summary>
        /// Creates a <see cref="CollectionConverter{T, TCollection}"/> using the specified underlying converter.
        /// </summary>
        /// <param name="underlying">the <see cref="ValueConverter{T}"/> to use to convert the values</param>
        public CollectionConverter(ValueConverter<T> underlying)
            => BaseConverter = underlying;

        /// <summary>
        /// Gets the converter for the collection's value type.
        /// </summary>
        protected ValueConverter<T> BaseConverter { get; }
        /// <summary>
        /// Creates a collection of type <typeparamref name="TCollection"/> using the <paramref name="size"/> and
        /// <paramref name="parent"/>.
        /// </summary>
        /// <param name="size">the initial size of the collecion</param>
        /// <param name="parent">the object that will own the new collection</param>
        /// <returns>a new instance of <typeparamref name="TCollection"/></returns>
        /// <seealso cref="ValueConverter{T}.FromValue(Value, object)"/>
        protected virtual TCollection Create(int size, object parent)
            => Activator.CreateInstance<TCollection>();
        /// <summary>
        /// Populates the colleciton <paramref name="col"/> with the deserialized values from <paramref name="list"/>
        /// with the parent <paramref name="parent"/>.
        /// </summary>
        /// <param name="col">the collection to populate</param>
        /// <param name="list">the values to populate it with</param>
        /// <param name="parent">the object that will own the new objects</param>
        /// <seealso cref="ValueConverter{T}.FromValue(Value, object)"/>
        protected void PopulateFromValue(TCollection col, List list, object parent)
        {
            foreach (var it in list)
                col.Add(BaseConverter.FromValue(it, parent));
        }
        /// <summary>
        /// Deserializes a <see cref="List"/> in <paramref name="value"/> into a new <typeparamref name="TCollection"/>
        /// owned by <paramref name="parent"/>.
        /// </summary>
        /// <param name="value">the <see cref="List"/> to convert to a <typeparamref name="TCollection"/></param>
        /// <param name="parent">the object that will own the resulting <typeparamref name="TCollection"/></param>
        /// <returns>a new <typeparamref name="TCollection"/> holding the deserialized content of <paramref name="value"/></returns>
        /// <seealso cref="ValueConverter{T}.FromValue(Value, object)"/>
        public override TCollection FromValue(Value value, object parent)
        {
            if (!(value is List list)) throw new ArgumentException("Argument not a List", nameof(value));

            var col = Create(list.Count, parent);
            PopulateFromValue(col, list, parent);
            return col;
        }
        /// <summary>
        /// Serializes a <typeparamref name="TCollection"/> into a <see cref="List"/>.
        /// </summary>
        /// <param name="obj">the <typeparamref name="TCollection"/> to serialize</param>
        /// <param name="parent">the object owning <paramref name="obj"/></param>
        /// <returns>the <see cref="List"/> that <paramref name="obj"/> was serialized into</returns>
        /// <seealso cref="ValueConverter{T}.ToValue(T, object)"/>
        public override Value ToValue(TCollection obj, object parent)
            => Value.From(obj.Select(t => BaseConverter.ToValue(t, parent)));
    }
    /// <summary>
    /// A <see cref="CollectionConverter{T, TCollection}"/> which default constructs a converter for use as the value converter.
    /// </summary>
    /// <typeparam name="T">the value type of the collection</typeparam>
    /// <typeparam name="TCollection">the type of the colleciton</typeparam>
    /// <typeparam name="TConverter">the type of the converter to use for <typeparamref name="T"/></typeparam>
    /// <seealso cref="CollectionConverter{T, TCollection}"/>
    public class CollectionConverter<T, TCollection, TConverter> : CollectionConverter<T, TCollection>
        where TCollection : ICollection<T>
        where TConverter : ValueConverter<T>, new()
    {
        /// <summary>
        /// Creates a <see cref="CollectionConverter{T, TCollection}"/> using the default converter for the
        /// element type. Equivalent to calling <see cref="CollectionConverter{T, TCollection}.CollectionConverter(ValueConverter{T})"/>
        /// with a default-constructed <typeparamref name="TConverter"/>.
        /// </summary>
        /// <seealso cref="CollectionConverter{T, TCollection}.CollectionConverter(ValueConverter{T})"/>
        public CollectionConverter() : base(new TConverter()) { }
    }

    public class ISetConverter<T> : CollectionConverter<T, ISet<T>>
    {
        public ISetConverter() : base() { }
        public ISetConverter(ValueConverter<T> underlying) : base(underlying) { }
        protected override ISet<T> Create(int size, object parent)
            => new HashSet<T>();
    }

    public class ListConverter<T> : CollectionConverter<T, List<T>>
    {
        public ListConverter() : base() { }
        public ListConverter(ValueConverter<T> underlying) : base(underlying) { }
        protected override List<T> Create(int size, object parent)
            => new List<T>(size);
    }

    public class IListConverter<T> : CollectionConverter<T, IList<T>>
    {
        public IListConverter() : base() { }
        public IListConverter(ValueConverter<T> underlying) : base(underlying) { }
        protected override IList<T> Create(int size, object parent)
            => new List<T>(size);
    }

}
