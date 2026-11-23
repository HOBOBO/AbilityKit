using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Logging;

namespace AbilityKit.Ability.World.DI
{
    /// <summary>
    /// 保存世界服务注册并拥有根容器的单例，不接管调用方创建的作用域或瞬时实例。
    /// </summary>
    public sealed class WorldContainer : IWorldResolver, IWorldServiceContainer, IDisposable
    {
        private readonly Dictionary<Type, WorldServiceDescriptor> _map;
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly InitializationTracker _initialization = new InitializationTracker();
        private readonly List<object> _singletonDisposeOrder = new List<object>(32);
        private readonly HashSet<object> _singletonDisposeSet = new HashSet<object>(ReferenceEqualityComparer.Instance);
        [ThreadStatic] private static Stack<Type> _singletonCreationStack;
        [ThreadStatic] private static Stack<RootCreation> _rootCreationStack;
        [ThreadStatic] private static Stack<ResolutionFrame> _resolveStack;
        private bool _disposing;
        private bool _disposed;

        /// <summary>
        /// 复制服务注册；同一契约出现多次时以最后一条为准，不在构造时创建实例。
        /// </summary>
        public WorldContainer(IEnumerable<WorldServiceDescriptor> descriptors)
        {
            _map = new Dictionary<Type, WorldServiceDescriptor>();
            foreach (var d in descriptors)
            {
                _map[d.ServiceType] = d;
            }
        }

        /// <summary>
        /// 返回显式注册契约的只读视图，不加入容器自身的内建解析入口。
        /// </summary>
        public IReadOnlyCollection<Type> RegisteredServiceTypes => _map.Keys;

        /// <summary>
        /// 检查显式注册或容器自身的内建入口；返回真不表示可从当前生命周期解析。
        /// </summary>
        public bool IsRegistered(Type serviceType)
        {
            if (serviceType == null) return false;
            if (serviceType == typeof(IWorldServiceContainer)) return true;
            if (serviceType == typeof(WorldContainer)) return true;
            return _map.ContainsKey(serviceType);
        }

        /// <summary>
        /// 返回显式注册的描述符；未注册或请求内建容器入口时返回默认值。
        /// </summary>
        public WorldServiceDescriptor GetDescriptor(Type serviceType)
        {
            if (serviceType == typeof(IWorldServiceContainer) || serviceType == typeof(WorldContainer))
            {
                return default;
            }
            if (_map.TryGetValue(serviceType, out var descriptor))
            {
                return descriptor;
            }
            return default;
        }

        /// <summary>
        /// 创建未播种的作用域；调用方须在根容器之前释放它。
        /// </summary>
        public WorldScope CreateScope()
        {
            ThrowIfDisposed();
            return new WorldScope(this);
        }

        /// <summary>
        /// 创建作用域并同步配置外部播种实例；作用域和播种实例均由调用方负责释放。
        /// </summary>
        public WorldScope CreateScope(Action<IWorldScopeSeeder> configure)
        {
            ThrowIfDisposed();
            var scope = new WorldScope(this);
            if (configure != null)
            {
                configure(new ScopeSeeder(scope));
            }
            return scope;
        }

        private sealed class ScopeSeeder : IWorldScopeSeeder
        {
            private readonly WorldScope _scope;

            public ScopeSeeder(WorldScope scope)
            {
                _scope = scope;
            }

            public IWorldScopeSeeder Seed(Type serviceType, object instance)
            {
                _scope.SeedInstance(serviceType, instance);
                return this;
            }

            public IWorldScopeSeeder Seed<TService>(TService instance)
            {
                _scope.SeedInstance(typeof(TService), instance);
                return this;
            }
        }

        /// <summary>
        /// 解析根容器中的服务；拒绝作用域服务，创建失败时传播异常。
        /// </summary>
        public object Resolve(Type serviceType)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            _resolveStack ??= new Stack<ResolutionFrame>(8);
            _resolveStack.Push(new ResolutionFrame(this, serviceType));
            try
            {
                if (serviceType == typeof(IWorldServiceContainer)) return this;
                if (serviceType == typeof(WorldContainer)) return this;

                if (!_map.TryGetValue(serviceType, out var descriptor))
                {
                    throw new InvalidOperationException($"Service not registered: {serviceType.FullName}. Resolve chain: {FormatResolveChain()}");
                }

                if (descriptor.Lifetime == WorldLifetime.Singleton)
                {
                    if (_singletons.TryGetValue(serviceType, out var cached)) return cached;

                    EnterRootCreation(serviceType);
                    _singletonCreationStack ??= new Stack<Type>(4);
                    _singletonCreationStack.Push(serviceType);
                    try
                    {
                        var created = descriptor.Factory(this);
                        if (descriptor.Ownership == WorldServiceOwnership.Container)
                        {
                            _initialization.EnsureInitialized(created, this);
                        }
                        _singletons[serviceType] = created;

                        if (descriptor.Ownership == WorldServiceOwnership.Container &&
                            created != null &&
                            _singletonDisposeSet.Add(created))
                        {
                            _singletonDisposeOrder.Add(created);
                        }

                        return created;
                    }
                    finally
                    {
                        _singletonCreationStack.Pop();
                        _rootCreationStack.Pop();
                    }
                }

                if (descriptor.Lifetime == WorldLifetime.Transient)
                {
                    EnterRootCreation(serviceType);
                    try
                    {
                        var created = descriptor.Factory(this);
                        if (descriptor.Ownership == WorldServiceOwnership.Container)
                            InitTransient(created, this);
                        return created;
                    }
                    finally
                    {
                        _rootCreationStack.Pop();
                    }
                }

                if (_singletonCreationStack != null && _singletonCreationStack.Count > 0)
                {
                    var singleton = _singletonCreationStack.Peek();
                    throw new InvalidOperationException($"Singleton service '{singleton.FullName}' cannot resolve scoped service '{serviceType.FullName}'. Scoped services must not be captured by singletons. Resolve chain: {FormatResolveChain()}");
                }

                throw new InvalidOperationException($"Cannot resolve scoped service from root container: {serviceType.FullName}");
            }
            finally
            {
                _resolveStack.Pop();
            }
        }

        /// <summary>
        /// 按泛型契约从根容器解析服务，沿用非泛型入口的生命周期与异常规则。
        /// </summary>
        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>
        /// 尝试从根容器解析；循环依赖异常仍会传播，其他失败可能表现为返回假。
        /// </summary>
        public bool TryResolve(Type serviceType, out object instance)
        {
            try
            {
                instance = Resolve(serviceType);
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Circular dependency detected."))
            {
                throw;
            }
            catch
            {
                instance = null;
                return false;
            }
        }

        /// <summary>
        /// 尝试解析可赋值给泛型契约的实例，沿用非泛型入口的异常规则。
        /// </summary>
        public bool TryResolve<T>(out T instance)
        {
            if (TryResolve(typeof(T), out var obj) && obj is T t)
            {
                instance = t;
                return true;
            }

            instance = default;
            return false;
        }

        internal object ResolveScoped(Type serviceType, WorldScope scope)
        {
            ThrowIfDisposed();

            _resolveStack ??= new Stack<ResolutionFrame>(8);

            if (!_map.TryGetValue(serviceType, out var descriptor))
            {
                throw new InvalidOperationException($"Service not registered: {serviceType.FullName}. Resolve chain: {FormatResolveChain()}");
            }

            /*
             * 用实际解析器和服务契约识别循环：不同作用域可创建同一契约，别名也可解析其实现类型。
             */
            foreach (var frame in _resolveStack)
            {
                if (ReferenceEquals(frame.Resolver, scope) && frame.ServiceType == serviceType)
                    throw new InvalidOperationException($"Circular dependency detected. Resolve chain: {FormatResolveChain()} -> {serviceType.FullName}");
            }

            _resolveStack.Push(new ResolutionFrame(scope, serviceType));
            try
            {
                switch (descriptor.Lifetime)
                {
                    case WorldLifetime.Singleton:
                        return Resolve(serviceType);
                    case WorldLifetime.Scoped:
                        return scope.GetOrCreate(serviceType, () =>
                        {
                            var created = descriptor.Factory(scope);
                            if (descriptor.Ownership == WorldServiceOwnership.Container)
                                scope.EnsureInitialized(created);
                            return created;
                        }, descriptor.Ownership == WorldServiceOwnership.Container);
                    case WorldLifetime.Transient:
                        var created = descriptor.Factory(scope);
                        if (descriptor.Ownership == WorldServiceOwnership.Container)
                            InitTransient(created, scope);
                        return created;
                    default:
                        throw new InvalidOperationException($"Unknown lifetime: {descriptor.Lifetime}");
                }
            }
            finally
            {
                _resolveStack.Pop();
            }
        }

        private static string FormatResolveChain()
        {
            if (_resolveStack == null || _resolveStack.Count == 0) return "<empty>";

            var frames = _resolveStack.ToArray();
            var chain = new System.Text.StringBuilder(128);
            for (int index = frames.Length - 1; index >= 0; index--)
            {
                var serviceType = frames[index].ServiceType;
                if (chain.Length > 0) chain.Append(" -> ");
                chain.Append(serviceType.FullName ?? serviceType.Name);
            }
            return chain.ToString();
        }

        /// <summary>
        /// 记录参与一次解析的容器或作用域，以及被请求的服务契约。
        /// </summary>
        private readonly struct ResolutionFrame
        {
            public readonly IWorldResolver Resolver;
            public readonly Type ServiceType;

            public ResolutionFrame(IWorldResolver resolver, Type serviceType)
            {
                Resolver = resolver;
                ServiceType = serviceType;
            }
        }

        private readonly struct RootCreation
        {
            public readonly WorldContainer Container;
            public readonly Type ServiceType;

            public RootCreation(WorldContainer container, Type serviceType)
            {
                Container = container;
                ServiceType = serviceType;
            }
        }

        private void EnterRootCreation(Type serviceType)
        {
            _rootCreationStack ??= new Stack<RootCreation>(8);
            foreach (var frame in _rootCreationStack)
            {
                if (ReferenceEquals(frame.Container, this) && frame.ServiceType == serviceType)
                    throw new InvalidOperationException($"Circular dependency detected. Resolve chain: {FormatResolveChain()}");
            }
            _rootCreationStack.Push(new RootCreation(this, serviceType));
        }

        private static void InitTransient(object instance, IWorldResolver services)
        {
            if (instance is IWorldInitializable init) init.OnInit(services);
        }

        /// <summary>
        /// 按实例身份跟踪受管服务初始化，只有回调成功后才保留完成状态。
        /// </summary>
        internal sealed class InitializationTracker
        {
            private enum State
            {
                InProgress,
                Completed
            }

            private readonly Dictionary<object, State> _states =
                new Dictionary<object, State>(ReferenceEqualityComparer.Instance);

            internal void EnsureInitialized(object instance, IWorldResolver services)
            {
                if (!(instance is IWorldInitializable initializable)) return;
                if (_states.TryGetValue(instance, out var state))
                {
                    if (state == State.Completed) return;
                    throw new InvalidOperationException(
                        $"Circular dependency detected. Service instance '{instance.GetType().FullName}' is being initialized. Resolve chain: {FormatResolveChain()}");
                }

                _states.Add(instance, State.InProgress);
                try
                {
                    initializable.OnInit(services);
                    _states[instance] = State.Completed;
                }
                catch
                {
                    _states.Remove(instance);
                    throw;
                }
            }

            internal void Clear() => _states.Clear();
        }

        private void TryDeinit(object instance, IWorldResolver services)
        {
            if (instance == null) return;

            if (instance is IWorldDeinitializable deinit)
            {
                deinit.OnDeinit(services);
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorldContainer));
        }

        /// <summary>
        /// 逆登记顺序释放受管单例；释放回调再次调用本方法时直接返回。
        /// </summary>
        public void Dispose()
        {
            if (_disposed || _disposing) return;
            _disposing = true;

            try
            {
                for (int index = _singletonDisposeOrder.Count - 1; index >= 0; index--)
                {
                    var instance = _singletonDisposeOrder[index];
                    try
                    {
                        TryDeinit(instance, this);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[WorldContainer] singleton deinit failed");
                    }

                    try
                    {
                        if (instance is IDisposable disposable) disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[WorldContainer] singleton dispose failed");
                    }
                }

                _disposed = true;
                _singletonDisposeOrder.Clear();
                _singletonDisposeSet.Clear();
                _initialization.Clear();
                _singletons.Clear();
                _map.Clear();
            }
            finally
            {
                _disposing = false;
            }
        }
    }
}
