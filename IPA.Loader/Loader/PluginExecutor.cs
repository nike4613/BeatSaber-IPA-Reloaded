using IPA.Logging;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace IPA.Loader
{
    internal class PluginExecutor
    {
        public enum Special
        {
            None, Self, Bare
        }

        public PluginMetadata Metadata { get; }
        public Special SpecialType { get; }
        public PluginExecutor(PluginMetadata meta, Special specialType = Special.None)
        {
            Metadata = meta;
            SpecialType = specialType;
            if (specialType != Special.None)
            {
                CreatePlugin = m => null;
                LifecycleEnable = o => Task.CompletedTask;
                LifecycleDisable = o => Task.CompletedTask;
            }
            else
                PrepareDelegates();
        }


        public object Instance { get; private set; } = null;
        private Func<PluginMetadata, object> CreatePlugin { get; set; }
        private Func<object, Task> LifecycleEnable { get; set; }
        private Func<object, Task> LifecycleDisable { get; set; }

        public void Create()
        {
            if (Instance != null) return;
            Instance = CreatePlugin(Metadata);
        }

        public Task Enable() => LifecycleEnable(Instance);
        public Task Disable() => LifecycleDisable(Instance);


        private void PrepareDelegates()
        { // TODO: use custom exception types or something
            PluginLoader.Load(Metadata);
            var type = Metadata.Assembly.GetType(Metadata.PluginType.FullName);

            CreatePlugin = MakeCreateFunc(type, Metadata.Name);
            LifecycleEnable = MakeLifecycleEnableFunc(type, Metadata.Name);
            LifecycleDisable = MakeLifecycleDisableFunc(type, Metadata.Name);
        }

        private static Func<PluginMetadata, object> MakeCreateFunc(Type type, string name)
        { // TODO: what do i want the visibiliy of Init methods to be?
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .Select(c => (c, attr: c.GetCustomAttribute<InitAttribute>()))
                            .NonNull(t => t.attr)
                            .OrderByDescending(t => t.c.GetParameters().Length)
                            .Select(t => t.c).ToArray();
            if (ctors.Length > 1)
                Logger.Loader.Warn($"Plugin {name} has multiple [Init] constructors. Picking the one with the most parameters.");

            bool usingDefaultCtor = false;
            var ctor = ctors.FirstOrDefault();
            if (ctor == null)
            { // this is a normal case
                usingDefaultCtor = true;
                ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                    throw new InvalidOperationException($"{type.FullName} does not expose a public default constructor and has no constructors marked [Init]");
            }

            var initMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .Select(m => (m, attr: m.GetCustomAttribute<InitAttribute>()))
                                .NonNull(t => t.attr).Select(t => t.m).ToArray();
            // verify that they don't have lifecycle attributes on them
            foreach (var method in initMethods)
            {
                var attrs = method.GetCustomAttributes(typeof(IEdgeLifecycleAttribute), false);
                if (attrs.Length != 0)
                    throw new InvalidOperationException($"Method {method} on {type.FullName} has both an [Init] attribute and a lifecycle attribute.");
            }

            var metaParam = Expression.Parameter(typeof(PluginMetadata), "meta");
            var objVar = Expression.Variable(type, "objVar");
            var persistVar = Expression.Variable(typeof(object), "persistVar");
            var createExpr = Expression.Lambda<Func<PluginMetadata, object>>(
                Expression.Block(new[] { objVar, persistVar },
                    initMethods
                        .Select(m => PluginInitInjector.InjectedCallExpr(m.GetParameters(), metaParam, persistVar, es => Expression.Call(objVar, m, es)))
                        .Prepend(Expression.Assign(objVar,
                            usingDefaultCtor
                                ? Expression.New(ctor)
                                : PluginInitInjector.InjectedCallExpr(ctor.GetParameters(), metaParam, persistVar, es => Expression.New(ctor, es))))
                        .Append(Expression.Convert(objVar, typeof(object)))),
                metaParam);
            // TODO: since this new system will be doing a fuck load of compilation, maybe add FastExpressionCompiler
            return createExpr.Compile();
        }
        // TODO: make enable and disable able to take a bool indicating which it is
        private static Func<object, Task> MakeLifecycleEnableFunc(Type type, string name)
        {
            var noEnableDisable = type.GetCustomAttribute<NoEnableDisableAttribute>() is not null;
            var enableMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Select(m => (m, attrs: m.GetCustomAttributes(typeof(IEdgeLifecycleAttribute), false)))
                                    .Select(t => (t.m, attrs: t.attrs.Cast<IEdgeLifecycleAttribute>()))
                                    .Where(t => t.attrs.Any(a => a.Type == EdgeLifecycleType.Enable))
                                    .Select(t => t.m).ToArray();
            if (enableMethods.Length == 0)
            {
                if (!noEnableDisable)
                    Logger.Loader.Notice($"Plugin {name} has no methods marked [OnStart] or [OnEnable]. Is this intentional?");
                return o => Task.CompletedTask;
            }

            var taskMethods = new List<MethodInfo>();
            var nonTaskMethods = new List<MethodInfo>();
            foreach (var m in enableMethods)
            {
                if (m.GetParameters().Length > 0)
                    throw new InvalidOperationException($"Method {m} on {type.FullName} is marked [OnStart] or [OnEnable] and has parameters.");
                if (m.ReturnType != typeof(void))
                {
                    if (typeof(Task).IsAssignableFrom(m.ReturnType))
                    {
                        taskMethods.Add(m);
                        continue;
                    }

                    Logger.Loader.Warn($"Method {m} on {type.FullName} is marked [OnStart] or [OnEnable] and returns a non-Task value. It will be ignored.");
                }

                nonTaskMethods.Add(m);
            }

            Expression<Func<Task>> completedTaskDel = () => Task.CompletedTask;
            var getCompletedTask = completedTaskDel.Body;
            var taskWhenAll = typeof(Task).GetMethod(nameof(Task.WhenAll), new[] { typeof(Task[]) });

            var objParam = Expression.Parameter(typeof(object), "obj");
            var instVar = Expression.Variable(type, "inst");
            var createExpr = Expression.Lambda<Func<object, Task>>(
                Expression.Block(new[] { instVar },
                    nonTaskMethods
                        .Select(m => (Expression)Expression.Call(instVar, m))
                        .Prepend(Expression.Assign(instVar, Expression.Convert(objParam, type)))
                        .Append(
                            taskMethods.Count == 0
                                ? getCompletedTask
                                : Expression.Call(taskWhenAll,
                                    Expression.NewArrayInit(typeof(Task),
                                        taskMethods.Select(m =>
                                            (Expression)Expression.Convert(Expression.Call(instVar, m), typeof(Task))))))),
                objParam);
            return createExpr.Compile();
        }
        private static Func<object, Task> MakeLifecycleDisableFunc(Type type, string name)
        {
            var noEnableDisable = type.GetCustomAttribute<NoEnableDisableAttribute>() is not null;
            var disableMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Select(m => (m, attrs: m.GetCustomAttributes(typeof(IEdgeLifecycleAttribute), false)))
                                    .Select(t => (t.m, attrs: t.attrs.Cast<IEdgeLifecycleAttribute>()))
                                    .Where(t => t.attrs.Any(a => a.Type == EdgeLifecycleType.Disable))
                                    .Select(t => t.m).ToArray();
            if (disableMethods.Length == 0)
            {
                if (!noEnableDisable)
                    Logger.Loader.Notice($"Plugin {name} has no methods marked [OnExit] or [OnDisable]. Is this intentional?");
                return o => Task.CompletedTask;
            }

            var taskMethods = new List<MethodInfo>();
            var nonTaskMethods = new List<MethodInfo>();
            foreach (var m in disableMethods)
            {
                if (m.GetParameters().Length > 0)
                    throw new InvalidOperationException($"Method {m} on {type.FullName} is marked [OnExit] or [OnDisable] and has parameters.");
                if (m.ReturnType != typeof(void))
                {
                    if (typeof(Task).IsAssignableFrom(m.ReturnType))
                    {
                        taskMethods.Add(m);
                        continue;
                    }

                    Logger.Loader.Warn($"Method {m} on {type.FullName} is marked [OnExit] or [OnDisable] and returns a non-Task value. It will be ignored.");
                }

                nonTaskMethods.Add(m);
            }

            Expression<Func<Task>> completedTaskDel = () => Task.CompletedTask;
            var getCompletedTask = completedTaskDel.Body;
            var taskWhenAll = typeof(Task).GetMethod(nameof(Task.WhenAll), new[] { typeof(Task[]) });

            var objParam = Expression.Parameter(typeof(object), "obj");
            var instVar = Expression.Variable(type, "inst");
            var createExpr = Expression.Lambda<Func<object, Task>>(
                Expression.Block(new[] { instVar },
                    nonTaskMethods
                        .Select(m => (Expression)Expression.Call(instVar, m))
                        .Prepend(Expression.Assign(instVar, Expression.Convert(objParam, type)))
                        .Append(
                            taskMethods.Count == 0
                                ? getCompletedTask
                                : Expression.Call(taskWhenAll,
                                    Expression.NewArrayInit(typeof(Task),
                                        taskMethods.Select(m =>
                                            (Expression)Expression.Convert(Expression.Call(instVar, m), typeof(Task))))))),
                objParam);
            return createExpr.Compile();
        }
    }
}