using IPA.Config.Data;
using IPA.Config.Stores.Attributes;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.IO;
using Boolean = IPA.Config.Data.Boolean;
using System.Collections;
using IPA.Utilities;
using System.ComponentModel;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.AssemblyVisibilityTarget)]

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        private static readonly Dictionary<Type, (GeneratedStoreCreator ctor, Type type)> generatedCreators = new Dictionary<Type, (GeneratedStoreCreator ctor, Type type)>();

        public static T Create<T>() where T : class => (T)Create(typeof(T));

        public static IConfigStore Create(Type type) => Create(type, null);

        private static readonly MethodInfo CreateGParent = 
            typeof(GeneratedStoreImpl).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static, null, 
                                             CallingConventions.Any, new[] { typeof(IGeneratedStore) }, Array.Empty<ParameterModifier>());
        internal static T Create<T>(IGeneratedStore parent) where T : class => (T)Create(typeof(T), parent);

        private static IConfigStore Create(Type type, IGeneratedStore parent)
            => GetCreator(type)(parent);

        internal static GeneratedStoreCreator GetCreator(Type t)
        {
            if (generatedCreators.TryGetValue(t, out var gen))
                return gen.ctor;
            else
            {
                gen = MakeCreator(t);
                generatedCreators.Add(t, gen);
                return gen.ctor;
            }
        }

        internal static Type GetGeneratedType(Type t)
        {
            if (generatedCreators.TryGetValue(t, out var gen))
                return gen.type;
            else
            {
                gen = MakeCreator(t);
                generatedCreators.Add(t, gen);
                return gen.type;
            }
        }

        internal const string GeneratedAssemblyName = "IPA.Config.Generated";

        private static AssemblyBuilder assembly = null;
        private static AssemblyBuilder Assembly
        {
            get
            {
                if (assembly == null)
                {
                    var name = new AssemblyName(GeneratedAssemblyName);
                    assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
                }

                return assembly;
            }
        }

        internal static void DebugSaveAssembly(string file)
        {
            Assembly.Save(file);
        }

        private static ModuleBuilder module = null;
        private static ModuleBuilder Module
        {
            get
            {
                if (module == null)
                    module = Assembly.DefineDynamicModule(Assembly.GetName().Name, Assembly.GetName().Name + ".dll");

                return module;
            }
        }
    }
}
