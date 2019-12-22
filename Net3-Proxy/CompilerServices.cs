using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Runtime.CompilerServices
{

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class CallerMemberNameAttribute : Attribute
    {
    }

}
