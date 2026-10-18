using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Logging;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldScope : IWorldResolver, IServiceProvider, IDisposable
    {
        private readonly WorldContainer _root;

        // scope 拥有的实例：由容器工厂在本 scope 内创建，进 _disposeOrder，随 Dispose 释放。
        private readonly Dictionary<Type, object> _scoped = new Dictionary<Type, object>();

        // 播种的实例：建 scope 时由外部注入，scope 不拥有，Dispose 不碰。
        // 与 _scoped 物理隔离，使「拥有/不拥有」由字典归属强制区分，而非靠 _disposeOrder 的注释约定。
        private readonly Dictionary<Type, object> _seeded = new Dictionary<Type, object>();

        private readonly List<object> _disposeOrder = new List<object>(32);
        private readonly HashSet<object> _ownedInstances = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> _initialized = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private bool _disposed;

        internal WorldScope(WorldContainer root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public IWorldResolver Root => _root;

        public object Resolve(Type serviceType)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            // 直接调用核心逻辑，循环检测由 WorldContainer.ResolveScoped 统一处理
            return ResolveCore(serviceType);
        }

        private object ResolveCore(Type serviceType)
        {
            if (serviceType == typeof(IWorldServiceContainer)) return _root;
            if (serviceType == typeof(IWorldResolver)) return this;
            if (serviceType == typeof(IWorldScope)) return this;
            if (serviceType == typeof(WorldScope)) return this;

            // 播种实例优先：建 scope 时注入的外部输入（生命周期归调用方，scope 不拥有）。
            // 命中 _seeded 可让"播种但未在容器注册"的类型也能解析，且覆盖容器内同类型 scoped 工厂。
            if (_seeded.TryGetValue(serviceType, out var seeded)) return seeded;

            return _root.ResolveScoped(serviceType, this);
        }

        /// <summary>
        /// 把一个外部构造的实例播种进本 scope（建 scope 时由 seeder 调用）。
        ///
        /// 语义（见 MobaFlowSpec.md 播种机制）：
        /// - 播种=注入外部输入（如 per-battle 的 bootstrapper/gateway），其生命周期归<b>调用方</b>。
        /// - 播种实例写入独立的 <see cref="_seeded"/> 字典，<b>不</b>进 <see cref="_disposeOrder"/>：
        ///   <see cref="Dispose"/> 只释放 scope 自己创建的实例，绝不连带释放仍被别处持有的播种对象。
        /// - 播种会覆盖容器内同类型的 scoped 工厂（解析时 <see cref="_seeded"/> 优先命中）。
        /// </summary>
        internal void SeedInstance(Type serviceType, object instance)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (!serviceType.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    $"Seeded instance of type '{instance.GetType().FullName}' is not assignable to service type '{serviceType.FullName}'.",
                    nameof(instance));
            }

            // 写入独立的 _seeded：所有权与 scope 自建实例物理隔离，Dispose 不接管其释放。
            _seeded[serviceType] = instance;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) return null;
            return Resolve(serviceType);
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            if (serviceType == null)
            {
                instance = null;
                return false;
            }

            if (_disposed)
            {
                instance = null;
                return false;
            }

            // 播种实例优先：与 Resolve 对齐。播种但未在容器注册的类型也能被 TryResolve 命中，
            // 否则下面的 IsRegistered 短路会让"已播种却未注册"的类型错误地返回 false。
            if (_seeded.TryGetValue(serviceType, out instance)) return true;

            if (serviceType != typeof(IWorldServiceContainer)
                && serviceType != typeof(IWorldResolver)
                && serviceType != typeof(IWorldScope)
                && serviceType != typeof(WorldScope)
                && !_root.IsRegistered(serviceType))
            {
                instance = null;
                return false;
            }

            try
            {
                instance = Resolve(serviceType);
                return true;
            }
            catch (ObjectDisposedException)
            {
                instance = null;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                // 循环依赖等业务异常不应该被吞掉，应该传播出去让调用者知道真正的问题。
                // 只有"服务未注册"这种可恢复的异常才返回 false。
                // 检查是否是"服务未注册"异常（消息格式：Service not registered: xxx）
                if (ex.Message.StartsWith("Service not registered:"))
                {
                    Log.Warning($"[WorldScope] TryResolve failed (not registered). serviceType={serviceType}");
                    instance = null;
                    return false;
                }

                // 其他 InvalidOperationException（如循环依赖）重新抛出
                throw;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[WorldScope] TryResolve failed. serviceType={serviceType}");
                instance = null;
                return false;
            }
        }

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

        internal object GetOrCreate(Type type, Func<object> factory, bool ownsInstance)
        {
            if (_scoped.TryGetValue(type, out var cached)) return cached;
            var created = factory();
            _scoped[type] = created;
            if (ownsInstance && created != null && _ownedInstances.Add(created))
                _disposeOrder.Add(created);
            return created;
        }

        internal void TryInit(object instance)
        {
            if (instance != null && _initialized.Add(instance) && instance is IWorldInitializable init)
                init.OnInit(this);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private void TryDeinit(object instance)
        {
            if (instance == null) return;

            if (instance is IWorldDeinitializable deinit)
            {
                deinit.OnDeinit(this);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorldScope));
        }

        public void Dispose()
        {
            if (_disposed) return;

            for (int i = _disposeOrder.Count - 1; i >= 0; i--)
            {
                var instance = _disposeOrder[i];
                try
                {
                    TryDeinit(instance);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[WorldScope] scoped deinit failed");
                }

                try
                {
                    if (instance is IDisposable d) d.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[WorldScope] scoped dispose failed");
                }
            }

            _disposed = true;
            _disposeOrder.Clear();
            _ownedInstances.Clear();
            _initialized.Clear();
            _scoped.Clear();
            _seeded.Clear();
        }
    }
}
