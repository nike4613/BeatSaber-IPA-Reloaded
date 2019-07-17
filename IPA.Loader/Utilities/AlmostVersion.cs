using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SemVer;
using Version = SemVer.Version;

namespace IPA.Utilities
{
    public class AlmostVersion : IComparable<AlmostVersion>, IComparable<Version>
    {
        private Version semverForm = null;
        private string strForm = null;
        private StoredAs storedAs;

        public enum StoredAs
        {
            SemVer,
            String
        }

        public AlmostVersion(string vertext)
        {
            if (!TryParseFrom(vertext, StoredAs.SemVer))
                TryParseFrom(vertext, StoredAs.String);
        }

        public AlmostVersion(Version ver)
        {
            semverForm = ver;
            storedAs = StoredAs.SemVer;
        }

        public AlmostVersion(string vertext, StoredAs mode)
        {
            if (!TryParseFrom(vertext, mode))
                throw new ArgumentException($"{nameof(vertext)} could not be stored as {mode}!");
        }

        public AlmostVersion(string vertext, AlmostVersion copyMode)
        {
            if (copyMode == null)
                throw new ArgumentNullException(nameof(copyMode));

            if (!TryParseFrom(vertext, copyMode.storedAs))
                throw new ArgumentException($"{nameof(vertext)} could not be stored the same way as {copyMode}!");
        }

        private bool TryParseFrom(string str, StoredAs mode)
        {
            if (mode == StoredAs.SemVer)
                try
                {
                    semverForm = new Version(str, true);
                    storedAs = StoredAs.SemVer;
                    return true;
                }
                catch
                {
                    return false;
                }
            else
            {
                strForm = str;
                storedAs = StoredAs.String;
                return true;
            }
        }

        public string StringValue => strForm;

        public Version SemverValue => semverForm;

        public override string ToString() =>
            storedAs == StoredAs.SemVer ? semverForm.ToString() : strForm;

        public int CompareTo(AlmostVersion other)
        {
            if (other == null) return -1;
            if (storedAs != other.storedAs)
                throw new InvalidOperationException("Cannot compare AlmostVersions with different stores!");

            if (storedAs == StoredAs.SemVer)
                return semverForm.CompareTo(other.semverForm);
            else
                return strForm.CompareTo(other.strForm);
        }

        public int CompareTo(Version other)
        {
            if (storedAs != StoredAs.SemVer)
                throw new InvalidOperationException("Cannot compare a SemVer version with an AlmostVersion stored as a string!");

            return semverForm.CompareTo(other);
        }

        public override bool Equals(object obj)
        {
            return obj is AlmostVersion version &&
                   semverForm == version.semverForm &&
                   strForm == version.strForm &&
                   storedAs == version.storedAs;
        }

        public override int GetHashCode()
        {
            var hashCode = -126402897;
            hashCode = hashCode * -1521134295 + EqualityComparer<Version>.Default.GetHashCode(semverForm);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(strForm);
            hashCode = hashCode * -1521134295 + storedAs.GetHashCode();
            return hashCode;
        }

        public static bool operator==(AlmostVersion l, AlmostVersion r)
        {
            if (l.storedAs != r.storedAs) return false;
            if (l.storedAs == StoredAs.SemVer)
                return Utils.VersionCompareNoPrerelease(l.semverForm, r.semverForm) == 0;
            else
                return l.strForm == r.strForm;
        }

        public static bool operator!=(AlmostVersion l, AlmostVersion r) => !(l == r);

        // implicitly convertible from Version
        public static implicit operator AlmostVersion(Version ver) => new AlmostVersion(ver);

        // implicitly convertible to Version
        public static implicit operator Version(AlmostVersion av) => av.SemverValue;
    }
}
