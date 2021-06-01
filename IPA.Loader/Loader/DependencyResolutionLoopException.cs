using System;
using System.Diagnostics.CodeAnalysis;

namespace IPA.Loader
{
    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This is only thrown and caught in local code")]
    internal sealed class DependencyResolutionLoopException : Exception
    {
        public DependencyResolutionLoopException(string message) : base(message)
        {
        }

        public DependencyResolutionLoopException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public DependencyResolutionLoopException()
        {
        }
    }
}
