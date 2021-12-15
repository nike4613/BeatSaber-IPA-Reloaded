#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IPA.Logging;
using IPA.Utilities;
using IPA.AntiMalware;
#if NET4
using Expression = System.Linq.Expressions.Expression;
using ExpressionEx = System.Linq.Expressions.Expression;
#endif
#if NET3
using Net3_Proxy;
#endif

namespace IPA.Loader
{
    /// <summary>
    /// The type that handles value injecting into a plugin's initialization methods.
    /// </summary>
    /// <remarks>
    /// The default injectors and what they provide are shown in this table.
    /// <list type="table">
    /// <listheader>
    /// <term>Parameter Type</term>
    /// <description>Injected Value</description>
    /// </listheader>
    /// <item>
    /// <term><see cref="Logger"/></term>
    /// <description>A <see cref="StandardLogger"/> specialized for the plugin being injected</description>
    /// </item>
    /// <item>
    /// <term><see cref="PluginMetadata"/></term>
    /// <description>The <see cref="PluginMetadata"/> of the plugin being injected</description>
    /// </item>
    /// <item>
    /// <term><see cref="Config.Config"/></term>
    /// <description>A <see cref="Config.Config"/> object for the plugin being injected.
    /// <para>
    /// These parameters may have <see cref="Config.Config.NameAttribute"/> and <see cref="Config.Config.PreferAttribute"/> to control
    /// how it is constructed.
    /// </para>
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="IAntiMalware"/></term>
    /// <description>The <see cref="IAntiMalware"/> instance which should be used for any potentially dangerous files.</description>
    /// </item>
    /// </list>
    /// For all of the default injectors, only one of each will be generated, and any later parameters will recieve the same value as the first one.
    /// </remarks>
    public static class PluginInitInjector
    {

        /// <summary>
        /// A typed injector for a plugin's Init method. When registered, called for all associated types. If it returns null, the default for the type will be used.
        /// </summary>
        /// <param name="previous">the previous return value of the function, or <see langword="null"/> if never called for plugin.</param>
        /// <param name="param">the <see cref="ParameterInfo"/> of the parameter being injected.</param>
        /// <param name="meta">the <see cref="PluginMetadata"/> for the plugin being loaded.</param>
        /// <returns>the value to inject into that parameter.</returns>
        public delegate object? InjectParameter(object? previous, ParameterInfo param, PluginMetadata meta);

        /// <summary>
        /// A provider for parameter injectors to request injected values themselves.
        /// </summary>
        /// <remarks>
        /// Some injectors may look at attributes on the parameter to gain additional information about what it should provide.
        /// If an injector wants to allow end users to affect the things it requests, it may pass the parameter it is currently
        /// injecting for to this delegate along with a type override to select some other type.
        /// </remarks>
        /// <param name="forParam">the parameter that this is providing for.</param>
        /// <param name="typeOverride">an optional override for the parameter type.</param>
        /// <returns>the value that would otherwise be injected.</returns>
        public delegate object? InjectedValueProvider(ParameterInfo forParam, Type? typeOverride = null);

        /// <summary>
        /// A typed injector for a plugin's Init method. When registered, called for all associated types. If it returns null, the default for the type will be used.
        /// </summary>
        /// <param name="previous">the previous return value of the function, or <see langword="null"/> if never called for plugin.</param>
        /// <param name="param">the <see cref="ParameterInfo"/> of the parameter being injected.</param>
        /// <param name="meta">the <see cref="PluginMetadata"/> for the plugin being loaded.</param>
        /// <param name="provider">an <see cref="InjectedValueProvider"/> to allow the injector to request injected values.</param>
        /// <returns>the value to inject into that parameter.</returns>
        public delegate object? InjectParameterNested(object? previous, ParameterInfo param, PluginMetadata meta, InjectedValueProvider provider);

        /// <summary>
        /// Invokes the provider with <paramref name="param"/> and <typeparamref name="T"/> and casts the result to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">the type of object to be injected</typeparam>
        /// <param name="provider">the provider to invoke.</param>
        /// <param name="param">the parameter to provide for</param>
        /// <returns>the value requested, or <see langword="null"/>.</returns>
        public static T? Inject<T>(this InjectedValueProvider provider, ParameterInfo param)
            => (T?)provider?.Invoke(param, typeof(T));

        /// <summary>
        /// Adds an injector to be used when calling future plugins' Init methods.
        /// </summary>
        /// <param name="type">the type of the parameter.</param>
        /// <param name="injector">the function to call for injection.</param>
        public static void AddInjector(Type type, InjectParameter injector)
            => AddInjector(type, (pre, par, met, pro) => injector(pre, par, met));

        /// <summary>
        /// Adds an injector to be used when calling future plugins' Init methods.
        /// </summary>
        /// <param name="type">the type of the parameter.</param>
        /// <param name="injector">the function to call for injection.</param>
        public static void AddInjector(Type type, InjectParameterNested injector)
        {
            injectors.Add(new TypedInjector(type, injector));
        }

        private struct TypedInjector : IEquatable<TypedInjector>
        {
            public Type Type;
            public InjectParameterNested Injector;

            public TypedInjector(Type t, InjectParameterNested i)
            { Type = t; Injector = i; }

            public object? Inject(object? prev, ParameterInfo info, PluginMetadata meta, InjectedValueProvider provider)
                => Injector(prev, info, meta, provider);

            public bool Equals(TypedInjector other)
                => Type == other.Type && Injector == other.Injector;

            public override bool Equals(object obj) 
                => obj is TypedInjector i && Equals(i);


            public override int GetHashCode()
                => Type.GetHashCode() ^ Injector.GetHashCode();

            public static bool operator ==(TypedInjector a, TypedInjector b) => a.Equals(b);
            public static bool operator !=(TypedInjector a, TypedInjector b) => !a.Equals(b);
        }

        private static readonly List<TypedInjector> injectors = new()
        {
            new TypedInjector(typeof(Logger), (prev, param, meta, _) => prev ?? new StandardLogger(meta.Name)),
            new TypedInjector(typeof(PluginMetadata), (prev, param, meta, _) => prev ?? meta),
            new TypedInjector(typeof(Config.Config), (prev, param, meta, _) => prev ?? Config.Config.GetConfigFor(meta.Name, param)),
            new TypedInjector(typeof(IAntiMalware), (prev, param, meta, _) => prev ?? AntiMalwareEngine.Engine)
        };

        private static int? MatchPriority(Type target, Type source)
        {
            if (target == source) return int.MaxValue;
            if (!target.IsAssignableFrom(source)) return null;
            if (!target.IsInterface && !source.IsSubclassOf(target)) return int.MinValue;

            int value = int.MaxValue - 1;
            while (true)
            {
                if (source is null) return value;
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
        internal static Expression InjectedCallExpr(ParameterInfo[] initParams, Expression meta, Expression persistVar, Func<IEnumerable<Expression>, Expression> exprGen)
        {
            var arr = ExpressionEx.Variable(typeof(object[]), "initArr");
            return ExpressionEx.Block(new[] { arr },
                ExpressionEx.Assign(arr, Expression.Call(InjectMethod, Expression.Constant(initParams), meta, persistVar)),
                exprGen(initParams
                            .Select(p => p.ParameterType)
                            .Select((t, i) => (Expression)Expression.Convert(
                                Expression.ArrayIndex(arr, Expression.Constant(i)), t))));
        }

        private static object? InjectForParameter(
            Dictionary<TypedInjector, object?> previousValues, 
            PluginMetadata meta,
            ParameterInfo param,
            Type paramType,
            InjectedValueProvider provider)
        {
            var value = paramType.GetDefault();

            var toUse = injectors
                .Select(i => (inject: i, priority: MatchPriority(paramType, i.Type)))   // check match priority, combine it
                .NonNull(t => t.priority)                             // filter null priorities
                .Select(t => (t.inject, priority: t.priority!.Value)) // remove nullable
                .OrderByDescending(t => t.priority)                    // sort by value
                .Select(t => t.inject);                                // remove priority value

            // this tries injectors in order of closest match by type provided 
            foreach (var pair in toUse)
            {
                object? prev = null;
                if (previousValues.ContainsKey(pair))
                    prev = previousValues[pair];

                var val = pair.Inject(prev, param, meta, provider);

                previousValues[pair] = val;

                if (val == null) continue;
                value = val;
                break;
            }

            return value;
        }

        private class InjectedValueProviderWrapperImplementation
        {
            public Dictionary<TypedInjector, object?> PreviousValues { get; }

            public PluginMetadata Meta { get; }

            public InjectedValueProvider Provider { get; }

            public InjectedValueProviderWrapperImplementation(PluginMetadata meta)
            {
                Meta = meta;
                PreviousValues = new();
                Provider = Inject;
            }

            private object? Inject(ParameterInfo param, Type? typeOverride = null)
                => InjectForParameter(PreviousValues, Meta, param, typeOverride ?? param.ParameterType, Provider);
        }

        internal static object?[] Inject(ParameterInfo[] initParams, PluginMetadata meta, ref object? persist)
        {
            var initArgs = new List<object?>();

            var impl = persist as InjectedValueProviderWrapperImplementation;
            if (impl == null || impl.Meta != meta)
            {
                impl = new(meta);
                persist = impl;
            }

            foreach (var param in initParams)
            {
                var paramType = param.ParameterType;

                var value = InjectForParameter(impl.PreviousValues, meta, param, paramType, impl.Provider);

                initArgs.Add(value);
            }

            return initArgs.ToArray();
        }
    }
}
