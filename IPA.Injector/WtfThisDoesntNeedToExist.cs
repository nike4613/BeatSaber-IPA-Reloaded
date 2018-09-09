using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Injector
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal class ForceAssemblyReferenceAttribute : Attribute
    {
        public ForceAssemblyReferenceAttribute(Type forcedType)
        {
            //not sure if these two lines are required since 
            //the type is passed to constructor as parameter, 
            //thus effectively being used
            Action<Type> noop = _ => { };
            noop(forcedType);
        }
    }
}
