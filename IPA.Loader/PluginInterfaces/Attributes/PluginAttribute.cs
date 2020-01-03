using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PluginAttribute : Attribute
    {
        // whenever this changes, PluginLoader.LoadMetadata must also change
        public RuntimeOptions RuntimeOptions { get; }
        public PluginAttribute(RuntimeOptions runtimeOptions)
        {
            RuntimeOptions = runtimeOptions;
        }
    }

    public enum RuntimeOptions
    {
        SingleStartInit,
        DynamicInit,

        // TODO: do I want this?
        SingleDynamicInit
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class InitAttribute : Attribute { }
}
