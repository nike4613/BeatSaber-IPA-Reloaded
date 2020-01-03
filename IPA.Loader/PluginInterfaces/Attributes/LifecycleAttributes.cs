using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA
{
    internal enum EdgeLifecycleType
    {
        Enable, Disable
    }

    internal interface IEdgeLifecycleAttribute
    {
        EdgeLifecycleType Type { get; }
    }

    // TODO: is there a better way to manage this mess?

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnEnableAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Enable;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnStartAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Enable;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnDisableAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Disable;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnExitAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Disable;
    }
}
