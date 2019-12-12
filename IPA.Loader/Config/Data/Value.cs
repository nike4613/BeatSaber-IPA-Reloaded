using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Data
{
    /// <summary>
    /// A base value type for config data abstract representations, to be serialized with an
    /// <see cref="IConfigProvider"/>. If a <see cref="Value"/> is <see langword="null"/>, then
    /// that represents just that: a <c>null</c> in whatever serialization is being used.
    /// Also contains factory functions for all derived types.
    /// </summary>
    public abstract class Value
    {
        /// <summary>
        /// Converts this <see cref="Value"/> into a human-readable format.
        /// </summary>
        /// <returns>a human-readable string containing the value provided</returns>
        public abstract override string ToString();

        /// <summary>
        /// Creates a Null <see cref="Value"/>.
        /// </summary>
        /// <returns><see langword="null"/></returns>
        public static Value Null() => null;

        /// <summary>
        /// Creates an empty <see cref="List"/>.
        /// </summary>
        /// <returns>an empty <see cref="List"/></returns>
        /// <seealso cref="From(IEnumerable{Value})"/>
        public static List List() => new List();
        /// <summary>
        /// Creates an empty <see cref="Map"/>.
        /// </summary>
        /// <returns>an empty <see cref="Map"/></returns>
        /// <seealso cref="From(IDictionary{string, Value})"/>
        /// <seealso cref="From(IEnumerable{KeyValuePair{string, Value}})"/>
        public static Map Map() => new Map();

        /// <summary>
        /// Creates a new <see cref="Value"/> representing a <see cref="string"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="Data.Text"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="Text(string)"/>
        public static Text From(string val) => Text(val);
        /// <summary>
        /// Creates a new <see cref="Data.Text"/> object wrapping a <see cref="string"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="Data.Text"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="From(string)"/>
        public static Text Text(string val) => val == null ? null : new Text { Value = val };

        /// <summary>
        /// Creates a new <see cref="Value"/> wrapping a <see cref="long"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="Data.Integer"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="Integer(long)"/>
        public static Integer From(long val) => Integer(val);
        /// <summary>
        /// Creates a new <see cref="Data.Integer"/> wrapping a <see cref="long"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="Data.Integer"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="From(long)"/>
        public static Integer Integer(long val) => new Integer { Value = val };

        /// <summary>
        /// Creates a new <see cref="Value"/> wrapping a <see cref="double"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="FloatingPoint"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="Float(decimal)"/>
        public static FloatingPoint From(decimal val) => Float(val);
        /// <summary>
        /// Creates a new <see cref="FloatingPoint"/> wrapping a <see cref="decimal"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="FloatingPoint"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="From(decimal)"/>
        public static FloatingPoint Float(decimal val) => new FloatingPoint { Value = val };

        /// <summary>
        /// Creates a new <see cref="Value"/> wrapping a <see cref="bool"/>.
        /// </summary>
        /// <param name="val">the  value to wrap</param>
        /// <returns>a <see cref="Boolean"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="Bool(bool)"/>
        public static Boolean From(bool val) => Bool(val);
        /// <summary>
        /// Creates a new <see cref="Boolean"/> wrapping a <see cref="bool"/>.
        /// </summary>
        /// <param name="val">the value to wrap</param>
        /// <returns>a <see cref="Boolean"/> wrapping <paramref name="val"/></returns>
        /// <seealso cref="From(bool)"/>
        public static Boolean Bool(bool val) => new Boolean { Value = val };

        /// <summary>
        /// Creates a new <see cref="Data.List"/> holding the content of an <see cref="IEnumerable{T}"/>
        /// of <see cref="Value"/>.
        /// </summary>
        /// <param name="vals">the <see cref="Value"/>s to initialize the <see cref="Data.List"/> with</param>
        /// <returns>a <see cref="Data.List"/> containing the content of <paramref name="vals"/></returns>
        /// <seealso cref="List"/>
        public static List From(IEnumerable<Value> vals)
        {
            if (vals == null) return null;
            var l = List();
            l.AddRange(vals);
            return l;
        }

        /// <summary>
        /// Creates a new <see cref="Data.Map"/> holding the content of an <see cref="IDictionary{TKey, TValue}"/>
        /// of <see cref="string"/> to <see cref="Value"/>.
        /// </summary>
        /// <param name="vals">the dictionary of <see cref="Value"/>s to initialize the <see cref="Data.Map"/> wtih</param>
        /// <returns>a <see cref="Data.Map"/> containing the content of <paramref name="vals"/></returns>
        /// <seealso cref="Map"/>
        /// <seealso cref="From(IEnumerable{KeyValuePair{string, Value}})"/>
        public static Map From(IDictionary<string, Value> vals) => From(vals as IEnumerable<KeyValuePair<string, Value>>);

        /// <summary>
        /// Creates a new <see cref="Data.Map"/> holding the content of an <see cref="IEnumerable{T}"/>
        /// of <see cref="KeyValuePair{TKey, TValue}"/> of <see cref="string"/> to <see cref="Value"/>.
        /// </summary>
        /// <param name="vals">the enumerable of <see cref="KeyValuePair{TKey, TValue}"/> of name to <see cref="Value"/></param>
        /// <returns>a <see cref="Data.Map"/> containing the content of <paramref name="vals"/></returns>
        /// <seealso cref="Map"/>
        /// <seealso cref="From(IDictionary{string, Value})"/>
        public static Map From(IEnumerable<KeyValuePair<string, Value>> vals)
        {
            if (vals == null) return null;
            var m = Map();
            foreach (var v in vals) m.Add(v.Key, v.Value);
            return m;
        }
    }
}
