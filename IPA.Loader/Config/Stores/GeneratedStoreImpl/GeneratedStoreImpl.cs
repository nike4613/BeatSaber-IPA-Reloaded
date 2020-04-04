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
using System.Collections.Concurrent;
using IPA.Utilities.Async;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.AssemblyVisibilityTarget)]

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        public static T Create<T>() where T : class => (T)Create(typeof(T));

        public static IConfigStore Create(Type type) => Create(type, null);

        private static readonly MethodInfo CreateGParent = 
            typeof(GeneratedStoreImpl).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static, null, 
                                             CallingConventions.Any, new[] { typeof(IGeneratedStore) }, Array.Empty<ParameterModifier>());
        internal static T Create<T>(IGeneratedStore parent) where T : class => (T)Create(typeof(T), parent);

        private static IConfigStore Create(Type type, IGeneratedStore parent)
            => GetCreator(type)(parent);

        private static readonly SingleCreationValueCache<Type, (GeneratedStoreCreator ctor, Type type)> generatedCreators
            = new SingleCreationValueCache<Type, (GeneratedStoreCreator ctor, Type type)>();

        private static (GeneratedStoreCreator ctor, Type type) GetCreatorAndGeneratedType(Type t)
            => generatedCreators.GetOrAdd(t, MakeCreator);

        internal static GeneratedStoreCreator GetCreator(Type t)
            => GetCreatorAndGeneratedType(t).ctor;

        internal static Type GetGeneratedType(Type t)
            => GetCreatorAndGeneratedType(t).type;

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

        // TODO: does this need to be a SingleCreationValueCache or similar?
        private static readonly Dictionary<Type, Dictionary<Type, FieldInfo>> TypeRequiredConverters = new Dictionary<Type, Dictionary<Type, FieldInfo>>();
        private static void CreateAndInitializeConvertersFor(Type type, IEnumerable<SerializedMemberInfo> structure)
        {
            if (!TypeRequiredConverters.TryGetValue(type, out var converters))
            {
                var converterFieldType = Module.DefineType($"{type.FullName}<Converters>",
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.AnsiClass); // a static class

                var uniqueConverterTypes = structure.Where(m => m.HasConverter).Select(m => m.Converter).Distinct().ToArray();
                converters = new Dictionary<Type, FieldInfo>(uniqueConverterTypes.Length);

                foreach (var convType in uniqueConverterTypes)
                {
                    var field = converterFieldType.DefineField($"<converter>_{convType}", convType,
                        FieldAttributes.FamORAssem | FieldAttributes.InitOnly | FieldAttributes.Static);
                    converters.Add(convType, field);
                }

                var cctor = converterFieldType.DefineConstructor(MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
                {
                    var il = cctor.GetILGenerator();

                    foreach (var kvp in converters)
                    {
                        var typeCtor = kvp.Key.GetConstructor(Type.EmptyTypes);
                        il.Emit(OpCodes.Newobj, typeCtor);
                        il.Emit(OpCodes.Stsfld, kvp.Value);
                    }

                    il.Emit(OpCodes.Ret);
                }

                TypeRequiredConverters.Add(type, converters);

                converterFieldType.CreateType();
            }

            foreach (var member in structure.Where(m => m.HasConverter))
                member.ConverterField = converters[member.Converter];
        }
    }
}
