using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Data
{
    /// <summary>
    /// A <see cref="Value"/> representing a piece of text. The only reason this is not named 
    /// String is so that it doesn't conflict with <see cref="string"/>.
    /// </summary>
    public sealed class Text : Value
    {
        /// <summary>
        /// The actual value of this <see cref="Text"/> object.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Converts this <see cref="Data.Value"/> into a human-readable format.
        /// </summary>
        /// <returns>a quoted, unescaped string form of <see cref="Value"/></returns>
        public override string ToString() => $"\"{Value}\"";
    }

    /// <summary>
    /// A <see cref="Value"/> representing an integer. This may hold a <see cref="long"/>'s 
    /// worth of data.
    /// </summary>
    public sealed class Integer : Value
    {
        /// <summary>
        /// The actual value of the <see cref="Integer"/> object.
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// Converts this <see cref="Data.Value"/> into a human-readable format.
        /// </summary>
        /// <returns>the result of <c>Value.ToString()</c></returns>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// A <see cref="Value"/> representing a floating point value. This may hold a 
    /// <see cref="double"/>'s  worth of data.
    /// </summary>
    public sealed class FloatingPoint : Value
    {
        /// <summary>
        /// The actual value fo this <see cref="FloatingPoint"/> object.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Converts this <see cref="Data.Value"/> into a human-readable format.
        /// </summary>
        /// <returns>the result of <c>Value.ToString()</c></returns>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// A <see cref="Value"/> representing a boolean value.
    /// </summary>
    public sealed class Boolean : Value
    {
        /// <summary>
        /// The actual value fo this <see cref="Boolean"/> object.
        /// </summary>
        public bool Value { get; set; }

        /// <summary>
        /// Converts this <see cref="Data.Value"/> into a human-readable format.
        /// </summary>
        /// <returns>the result of <c>Value.ToString().ToLower()</c></returns>
        public override string ToString() => Value.ToString().ToLower();
    }
}
