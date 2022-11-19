#nullable enable
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
        /// Constructs an empty <see cref="Text"/> object.
        /// </summary>
        [Obsolete("Use the String constructor.")]
        public Text()
        {
            Value = null!;
        }

        /// <summary>
        /// Constructs a <see cref="Text"/> object containing the provided value.
        /// </summary>
        /// <param name="value">The value to construct with.</param>
        public Text(string value)
        {
            Value = value;
        }

        /// <summary>
        /// The actual value of this <see cref="Text"/> object.
        /// </summary>
        public string Value { get; init; }

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
        /// Constructs an empty <see cref="Integer"/> object.
        /// </summary>
        [Obsolete("Use the long constructor.")]
        public Integer()
        {
            Value = 0;
        }

        /// <summary>
        /// Constructs a <see cref="Integer"/> object containing the provided value.
        /// </summary>
        /// <param name="value">The value to construct with.</param>
        public Integer(long value)
        {
            Value = value;
        }

        /// <summary>
        /// The actual value of the <see cref="Integer"/> object.
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// Coerces this <see cref="Integer"/> into a <see cref="FloatingPoint"/>.
        /// </summary>
        /// <returns>a <see cref="FloatingPoint"/> representing the closest approximation of <see cref="Value"/></returns>
        public FloatingPoint AsFloat() => Float(Value);

        /// <summary>
        /// Converts this <see cref="Data.Value"/> into a human-readable format.
        /// </summary>
        /// <returns>the result of <c>Value.ToString()</c></returns>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// A <see cref="Value"/> representing a floating point value. This may hold a 
    /// <see cref="decimal"/>'s worth of data.
    /// </summary>
    public sealed class FloatingPoint : Value
    {
        /// <summary>
        /// Constructs an empty <see cref="FloatingPoint"/> object.
        /// </summary>
        [Obsolete("Use the long constructor.")]
        public FloatingPoint()
        {
            Value = 0;
        }

        /// <summary>
        /// Constructs a <see cref="FloatingPoint"/> object containing the provided value.
        /// </summary>
        /// <param name="value">The value to construct with.</param>
        public FloatingPoint(decimal value)
        {
            Value = value;
        }

        /// <summary>
        /// The actual value fo this <see cref="FloatingPoint"/> object.
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Coerces this <see cref="FloatingPoint"/> into an <see cref="Integer"/>.
        /// </summary>
        /// <returns>a <see cref="Integer"/> representing the closest approximation of <see cref="Value"/></returns>
        public Integer AsInteger() => Integer((long)Value);

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
        /// Constructs an empty <see cref="Boolean"/> object.
        /// </summary>
        [Obsolete("Use the long constructor.")]
        public Boolean()
        {
            Value = false;
        }

        /// <summary>
        /// Constructs a <see cref="Boolean"/> object containing the provided value.
        /// </summary>
        /// <param name="value">The value to construct with.</param>
        public Boolean(bool value)
        {
            Value = value;
        }

        /// <summary>
        /// The actual value fo this <see cref="Boolean"/> object.
        /// </summary>
        public bool Value { get; set; }


        /// <summary>
        /// Converts this <see cref="Data.Value"/> into a human-readable format.
        /// </summary>
        /// <returns>the result of <c>Value.ToString().ToLower()</c></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
            Justification = "ToLower is the desired display value.")]
        public override string ToString() => Value.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture);
    }
}
