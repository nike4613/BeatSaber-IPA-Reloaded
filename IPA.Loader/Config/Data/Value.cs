using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Data
{
    /// <summary>
    /// A base value type for config data abstract representations, to be serialized with an
    /// <see cref="IConfigProvider"/>.
    /// </summary>
    public abstract class Value
    {

    }

    // not String to prevent overlap with System.String
    /// <summary>
    /// A <see cref="Value"/> representing a piece of text. The only reason this is not named 
    /// String is so that it doesn't conflict with <see cref="string"/>.
    /// </summary>
    public sealed class Text : Value
    {

    }

    /// <summary>
    /// A <see cref="Value"/> representing an integer. This may hold a <see cref="long"/>'s 
    /// worth of data.
    /// </summary>
    public sealed class Integer : Value
    {

    }

    /// <summary>
    /// A <see cref="Value"/> representing a floating point value. This may hold a 
    /// <see cref="double"/>'s  worth of data.
    /// </summary>
    public sealed class FloatingPoint : Value
    {

    }

    /// <summary>
    /// A <see cref="Value"/> representing a boolean value.
    /// </summary>
    public sealed class Boolean : Value
    {

    }


}
