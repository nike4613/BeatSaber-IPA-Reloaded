using System;
using System.Collections.Generic;
using System.Linq;
using Hive.Versioning;
using HVersion = Hive.Versioning.Version;

namespace SemVer
{
    [Obsolete("Use Hive.Versioning.VersionRange instead.")]
    public class Range : IEquatable<Range>, IEquatable<VersionRange>
    {
        public VersionRange UnderlyingRange { get; }

        private Range(VersionRange real) => UnderlyingRange = real;

        public Range(string rangeSpec, bool loose = false) : this(new(rangeSpec))
            => _ = loose; // loose is ignored because Hive doesn't have an equivalent

        public static Range ForHiveRange(VersionRange real) => new(real);

        public bool IsSatisfied(Version version) => IsSatisfied(version.UnderlyingVersion);
        public bool IsSatisfied(HVersion version) => UnderlyingRange.Matches(version);
        public bool IsSatisfied(string versionString, bool loose = false) => IsSatisfied(new Version(versionString, loose));

        public IEnumerable<Version> Satisfying(IEnumerable<Version> versions) => versions.Where(IsSatisfied);
        public IEnumerable<string> Satisfying(IEnumerable<string> versions, bool loose = false)
            => versions.Where(v => IsSatisfied(v, loose));
        public Version? MaxSatisfying(IEnumerable<Version> versions) => Satisfying(versions).Max();
        public string? MaxSatisfying(IEnumerable<string> versionStrings, bool loose = false)
            => MaxSatisfying(ValidVersions(versionStrings, loose))?.ToString();
        public Range Intersect(Range other) => new(UnderlyingRange & other.UnderlyingRange); // the conjunction is the intersection
        public override string ToString() => UnderlyingRange.ToString();

        public bool Equals(Range? other) => UnderlyingRange.Equals(other?.UnderlyingRange);
        public bool Equals(VersionRange? other) => UnderlyingRange.Equals(other);
        public override bool Equals(object? obj)
            => obj switch
            {
                Range r => Equals(r),
                VersionRange vr => Equals(vr),
                _ => false
            };

        public static bool operator ==(Range? a, Range? b) => a?.Equals(b) ?? b is null;

        public static bool operator !=(Range? a, Range? b) => !(a == b);

        public override int GetHashCode() => UnderlyingRange.GetHashCode();

        public static bool IsSatisfied(string rangeSpec, string versionString, bool loose = false)
            => new Range(rangeSpec, loose).IsSatisfied(versionString, loose);
        public static IEnumerable<string> Satisfying(string rangeSpec, IEnumerable<string> versions, bool loose = false)
            => new Range(rangeSpec, loose).Satisfying(versions, loose);

        public static string? MaxSatisfying(string rangeSpec, IEnumerable<string> versions, bool loose = false)
            => new Range(rangeSpec, loose).MaxSatisfying(versions, loose);

        private IEnumerable<Version> ValidVersions(IEnumerable<string> versionStrings, bool loose)
        {
            foreach (string versionString in versionStrings)
            {
                Version? version = null;
                try
                {
                    version = new Version(versionString, loose);
                }
                catch (ArgumentException)
                {
                }

                if (version is not null)
                {
                    yield return version;
                }
            }
        }
    }
}
