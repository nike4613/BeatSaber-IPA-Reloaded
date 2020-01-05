using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IPA.Config;
using IPA.Logging;
using IPA.Utilities;
using System.Linq.Expressions;
#if NET3
using Net3_Proxy;
#endif

namespace IPA.Loader
{
    /// <summary>
    /// The type that handles value injecting into a plugin's initialization methods.
    /// </summary>
    public static class PluginInitInjector
    {

        /// <summary>
        /// A typed injector for a plugin's Init method. When registered, called for all associated types. If it returns null, the default for the type will be used.
        /// </summary>
        /// <param name="previous">the previous return value of the function, or <see langword="null"/> if never called for plugin.</param>
        /// <param name="param">the <see cref="ParameterInfo"/> of the parameter being injected.</param>
        /// <param name="meta">the <see cref="PluginMetadata"/> for the plugin being loaded.</param>
        /// <returns>the value to inject into that parameter.</returns>
        public delegate object InjectParameter(object previous, ParameterInfo param, PluginMetadata meta);

        /// <summary>
        /// Adds an injector to be used when calling future plugins' Init methods.
        /// </summary>
        /// <param name="type">the type of the parameter.</param>
        /// <param name="injector">the function to call for injection.</param>
        public static void AddInjector(Type type, InjectParameter injector)
        {
            injectors.Add(new TypedInjector(type, injector));
        }

        private struct TypedInjector : IEquatable<TypedInjector>
        {
            public Type Type;
            public InjectParameter Injector;

            public TypedInjector(Type t, InjectParameter i)
            { Type = t; Injector = i; }

            public object Inject(object prev, ParameterInfo info, PluginMetadata meta)
                => Injector(prev, info, meta);

            public bool Equals(TypedInjector other)
                => Type == other.Type && Injector == other.Injector;

            public override bool Equals(object obj) 
                => obj is TypedInjector i && Equals(i);


            public override int GetHashCode()
                => Type.GetHashCode() ^ Injector.GetHashCode();

            public static bool operator ==(TypedInjector a, TypedInjector b) => a.Equals(b);
            public static bool operator !=(TypedInjector a, TypedInjector b) => !a.Equals(b);
        }

        private static readonly List<TypedInjector> injectors = new List<TypedInjector>
        {
            new TypedInjector(typeof(Logger), (prev, param, meta) => prev ?? new StandardLogger(meta.Name)),
            new TypedInjector(typeof(PluginMetadata), (prev, param, meta) => prev ?? meta),
            new TypedInjector(typeof(Config.Config), (prev, param, meta) =>
            {
                if (prev != null) return prev;
                return Config.Config.GetConfigFor(meta.Name, param);
            })
        };

        private static int? MatchPriority(Type target, Type source)
        {
            if (target == source) return int.MaxValue;
            if (!target.IsAssignableFrom(source)) return null;
            if (!target.IsInterface && !source.IsSubclassOf(target)) return int.MinValue;

            int value = 0;
            while (true)
            {
                if (source == null) return value;
                if (target.IsInterface && source.GetInterfaces().Contains(target))
                    return value;
                else if (target == source)
                    return value;
                else
                {
                    value--; // lower priority
                    source = source.BaseType;
                }
            }
        }

        private static readonly MethodInfo InjectMethod = typeof(PluginInitInjector).GetMethod(nameof(Inject), BindingFlags.NonPublic | BindingFlags.Static);
        internal static Expression InjectedCallExpr(ParameterInfo[] initParams, Expression meta, ParameterExpression persistVar, Func<IEnumerable<Expression>, Expression> exprGen)
        {
            var arr = Expression.Variable(typeof(object[]), "initArr");
            return Expression.Block(new[] { arr },
                Expression.Assign(arr, Expression.Call(InjectMethod, Expression.Constant(initParams), meta, persistVar)),
                exprGen(initParams
                            .Select(p => p.ParameterType)
                            .Select((t, i) => Expression.Convert(
                                Expression.ArrayIndex(arr, Expression.Constant(i)), t))));
        }

        internal static object[] Inject(ParameterInfo[] initParams, PluginMetadata meta, ref object persist)
        {
            var initArgs = new List<object>();

            var previousValues = persist as Dictionary<TypedInjector, object>;
            if (previousValues == null)
            {
                previousValues = new Dictionary<TypedInjector, object>(injectors.Count);
                persist = previousValues;
            }

            foreach (var param in initParams)
            {
                var paramType = param.ParameterType;

                var value = paramType.GetDefault();

                var toUse = injectors.Select(i => (inject: i, priority: MatchPriority(paramType, i.Type)))  // check match priority, combine it
                                     .Where(t => t.priority != null)                                        // filter null priorities
                                     .Select(t => (t.inject, priority: t.priority.Value))                   // remove nullable
                                     .OrderByDescending(t => t.priority)                                    // sort by value
                                     .Select(t => t.inject);                                                // remove priority value

                // this tries injectors in order of closest match by type provided 
                foreach (var pair in toUse)
                {
                    object prev = null;
                    if (previousValues.ContainsKey(pair))
                        prev = previousValues[pair];

                    var val = pair.Inject(prev, param, meta);

                    if (previousValues.ContainsKey(pair))
                        previousValues[pair] = val;
                    else
                        previousValues.Add(pair, val);

                    if (val == null) continue;
                    value = val;
                    break;
                }

                initArgs.Add(value);
            }

            //init.Invoke(instance, initArgs.ToArray());
            return initArgs.ToArray();
        }
    }
}
