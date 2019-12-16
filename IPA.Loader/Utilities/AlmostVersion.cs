using IPA.Config.Data;
using IPA.Config.Stores;
using IPA.Config.Stores.Converters;
using System;
using System.Collections.Generic;
using Version = SemVer.Version;

namespace IPA.Utilities
{
    /// <summary>
    /// A type that wraps <see cref="Version"/> so that the string of the version is stored when the string is 
    /// not a valid <see cref="Version"/>.
    /// </summary>
    public class AlmostVersion : IComparable<AlmostVersion>, IComparable<Version>
    {
        /// <summary>
        /// Represents a storage type of either parsed <see cref="Version"/> object or raw <see cref="String"/>.
        /// </summary>
        public enum StoredAs
        {
            /// <summary>
            /// The version was stored as a <see cref="Version"/>.
            /// </summary>
            SemVer,
            /// <summary>
            /// The version was stored as a <see cref="String"/>.
            /// </summary>
            String
        }

        /// <summary>
        /// Creates a new <see cref="AlmostVersion"/> with the version string provided in <paramref name="vertext"/>.
        /// </summary>
        /// <param name="vertext">the version string to store</param>
        public AlmostVersion(string vertext)
        {
            if (!TryParseFrom(vertext, StoredAs.SemVer))
                TryParseFrom(vertext, StoredAs.String);
        }

        /// <summary>
        /// Creates an <see cref="AlmostVersion"/> from the <see cref="Version"/> provided in <paramref name="ver"/>.
        /// </summary>
        /// <param name="ver">the <see cref="Version"/> to store</param>
        public AlmostVersion(Version ver)
        {
            SemverValue = ver;
            StorageMode = StoredAs.SemVer;
        }

        /// <summary>
        /// Creates an <see cref="AlmostVersion"/> from the version string in <paramref name="vertext"/> stored using 
        /// the storage mode specified in <paramref name="mode"/>.
        /// </summary>
        /// <param name="vertext">the text to parse as an <see cref="AlmostVersion"/></param>
        /// <param name="mode">the storage mode to store the version in</param>
        public AlmostVersion(string vertext, StoredAs mode)
        {
            if (!TryParseFrom(vertext, mode))
                throw new ArgumentException($"{nameof(vertext)} could not be stored as {mode}!");
        }

        /// <summary>
        /// Creates a new <see cref="AlmostVersion"/> from the version string in <paramref name="vertext"/> stored the
        /// same way as the <see cref="AlmostVersion"/> passed in <paramref name="copyMode"/>.
        /// </summary>
        /// <param name="vertext">the text to parse as an <see cref="AlmostVersion"/></param>
        /// <param name="copyMode">an <see cref="AlmostVersion"/> to copy the storage mode of</param>
        public AlmostVersion(string vertext, AlmostVersion copyMode)
        {
            if (copyMode == null)
                throw new ArgumentNullException(nameof(copyMode));

            if (!TryParseFrom(vertext, copyMode.StorageMode))
                TryParseFrom(vertext, StoredAs.String); // silently parse differently
        }

        private bool TryParseFrom(string str, StoredAs mode)
        {
            if (mode == StoredAs.SemVer)
                try
                {
                    SemverValue = new Version(str, true);
                    StorageMode = StoredAs.SemVer;
                    return true;
                }
                catch
                {
                    return false;
                }
            else
            {
                StringValue = str;
                StorageMode = StoredAs.String;
                return true;
            }
        }

        /// <summary>
        /// The value of the <see cref="AlmostVersion"/> if it was stored as a <see cref="string"/>.
        /// </summary>
        /// <value>the stored value as a <see cref="string"/>, or <see langword="null"/> if not stored as a string.</value>
        public string StringValue { get; private set; } = null;

        /// <summary>
        /// The value of the <see cref="AlmostVersion"/> if it was stored as a <see cref="Version"/>.
        /// </summary>
        /// <value>the stored value as a <see cref="Version"/>, or <see langword="null"/> if not stored as a version.</value>
        public Version SemverValue { get; private set; } = null;

        /// <summary>
        /// The way the value is stored, whether it be as a <see cref="Version"/> or a <see cref="string"/>.
        /// </summary>
        /// <value>the storage mode used to store this value</value>
        public StoredAs StorageMode { get; private set; }

        // can I just <inheritdoc /> this?
        /// <summary>
        /// Gets a string representation of the current version. If the value is stored as a string, this returns it. If it is
        /// stored as a <see cref="Version"/>, it is equivalent to calling <see cref="Version.ToString"/>.
        /// </summary>
        /// <returns>a string representation of the current version</returns>
        /// <seealso cref="object.ToString"/>
        public override string ToString() =>
            StorageMode == StoredAs.SemVer ? SemverValue.ToString() : StringValue;

        /// <summary>
        /// Compares <see langword="this"/> to the <see cref="AlmostVersion"/> in <paramref name="other"/> using <see cref="Version.CompareTo(Version)"/>
        /// or <see cref="string.CompareTo(string)"/>, depending on the current store.
        /// </summary>
        /// <remarks>
        /// The storage methods of the two objects must be the same, or this will throw an <see cref="InvalidOperationException"/>.
        /// </remarks>
        /// <param name="other">the <see cref="AlmostVersion"/> to compare to</param>
        /// <returns>less than 0 if <paramref name="other"/> is considered bigger than <see langword="this"/>, 0 if equal, and greater than zero if smaller</returns>
        /// <seealso cref="CompareTo(Version)"/>
        public int CompareTo(AlmostVersion other)
        {
            if (other == null) return -1;
            if (StorageMode != other.StorageMode)
                throw new InvalidOperationException("Cannot compare AlmostVersions with different stores!");

            if (StorageMode == StoredAs.SemVer)
                return SemverValue.CompareTo(other.SemverValue);
            else
                return StringValue.CompareTo(other.StringValue);
        }

        /// <summary>
        /// Compares <see langword="this"/> to the <see cref="Version"/> in <paramref name="other"/> using <see cref="Version.CompareTo(Version)"/>.
        /// </summary>
        /// <remarks>
        /// The storage method of <see langword="this"/> must be <see cref="StoredAs.SemVer"/>, else an <see cref="InvalidOperationException"/> will
        /// be thrown.
        /// </remarks>
        /// <param name="other">the <see cref="Version"/> to compare to</param>
        /// <returns>less than 0 if <paramref name="other"/> is considered bigger than <see langword="this"/>, 0 if equal, and greater than zero if smaller</returns>
        /// <seealso cref="CompareTo(AlmostVersion)"/>
        public int CompareTo(Version other)
        {
            if (StorageMode != StoredAs.SemVer)
                throw new InvalidOperationException("Cannot compare a SemVer version with an AlmostVersion stored as a string!");

            return SemverValue.CompareTo(other);
        }

        /// <summary>
        /// Performs a strict equality check between <see langword="this"/> and <paramref name="obj"/>.
        /// </summary>
        /// <remarks>
        /// This may return <see langword="false"/> where <see cref="operator ==(AlmostVersion, AlmostVersion)"/> returns <see langword="true"/>
        /// </remarks>
        /// <param name="obj">the object to compare to</param>
        /// <returns><see langword="true"/> if they are equal, <see langword="false"/> otherwise</returns>
        /// <seealso cref="object.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is AlmostVersion version &&
                   SemverValue == version.SemverValue &&
                   StringValue == version.StringValue &&
                   StorageMode == version.StorageMode;
        }

        /// <summary>
        /// Default generated hash code function generated by VS.
        /// </summary>
        /// <returns>a value unique to each object, except those that are considered equal by <see cref="Equals(object)"/></returns>
        /// <seealso cref="object.GetHashCode"/>
        public override int GetHashCode()
        {
            var hashCode = -126402897;
            hashCode = hashCode * -1521134295 + EqualityComparer<Version>.Default.GetHashCode(SemverValue);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(StringValue);
            hashCode = hashCode * -1521134295 + StorageMode.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Compares two versions, only taking into account the numeric part of the version if they are stored as <see cref="Version"/>s,
        /// or strict equality if they are stored as <see cref="string"/>s.
        /// </summary>
        /// <remarks>
        /// This is a looser equality than <see cref="Equals(object)"/>, meaning that this may return <see langword="true"/> where <see cref="Equals(object)"/>
        /// does not.
        /// </remarks>
        /// <param name="l">the first value to compare</param>
        /// <param name="r">the second value to compare</param>
        /// <returns><see langword="true"/> if they are mostly equal, <see langword="false"/> otherwise</returns>
        /// <seealso cref="Equals(object)"/>
        public static bool operator==(AlmostVersion l, AlmostVersion r)
        {
            if (l is null && r is null) return true;
            if (l is null || r is null) return false;
            if (l.StorageMode != r.StorageMode) return false;
            if (l.StorageMode == StoredAs.SemVer)
                return Utils.VersionCompareNoPrerelease(l.SemverValue, r.SemverValue) == 0;
            else
                return l.StringValue == r.StringValue;
        }

        /// <summary>
        /// The opposite of <see cref="operator ==(AlmostVersion, AlmostVersion)"/>. Equivalent to <c>!(l == r)</c>.
        /// </summary>
        /// <param name="l">the first value to compare</param>
        /// <param name="r">the second value to compare</param>
        /// <returns><see langword="true"/> if they are not mostly equal, <see langword="false"/> otherwise</returns>
        /// <seealso cref="operator ==(AlmostVersion, AlmostVersion)"/>
        public static bool operator!=(AlmostVersion l, AlmostVersion r) => !(l == r);

        // implicitly convertible from Version
        /// <summary>
        /// Implicitly converts a <see cref="Version"/> to <see cref="AlmostVersion"/> using <see cref="AlmostVersion(Version)"/>.
        /// </summary>
        /// <param name="ver">the <see cref="Version"/> to convert</param>
        /// <seealso cref="AlmostVersion(Version)"/>
        public static implicit operator AlmostVersion(Version ver) => new AlmostVersion(ver);

        // implicitly convertible to Version
        /// <summary>
        /// Implicitly converts an <see cref="AlmostVersion"/> to <see cref="Version"/>, if applicable, using <see cref="SemverValue"/>.
        /// If not applicable, returns <see langword="null"/>
        /// </summary>
        /// <param name="av">the <see cref="AlmostVersion"/> to convert to a <see cref="Version"/></param>
        /// <seealso cref="SemverValue"/>
        public static implicit operator Version(AlmostVersion av) => av?.SemverValue;
    }

    /// <summary>
    /// A <see cref="ValueConverter{T}"/> for <see cref="AlmostVersion"/>s.
    /// </summary>
    public sealed class AlmostVersionConverter : ValueConverter<AlmostVersion>
    {
        /// <summary>
        /// Converts a <see cref="Text"/> node into an <see cref="AlmostVersion"/>.
        /// </summary>
        /// <param name="value">the <see cref="Text"/> node to convert</param>
        /// <param name="parent">the owner of the new object</param>
        /// <returns></returns>
        public override AlmostVersion FromValue(Value value, object parent)
            => new AlmostVersion(Converter<string>.Default.FromValue(value, parent));
        /// <summary>
        /// Converts an <see cref="AlmostVersion"/> to a <see cref="Text"/> node.
        /// </summary>
        /// <param name="obj">the <see cref="AlmostVersion"/> to convert</param>
        /// <param name="parent">the parent of <paramref name="obj"/></param>
        /// <returns>a <see cref="Text"/> node representing <paramref name="obj"/></returns>
        public override Value ToValue(AlmostVersion obj, object parent)
            => Value.From(obj.ToString());
    }
}
