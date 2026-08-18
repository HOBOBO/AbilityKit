using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Ability.Flow.Pooling
{
    /// <summary>
    /// Flow 包内部对象池入口，集中声明 Flow 高频对象的池键、默认配置、业务覆盖配置与租借/释放策略。
    /// </summary>
    public static class FlowPools
    {
        /// <summary>
        /// Flow 包默认对象池作用域名称。
        /// </summary>
        public const string ScopeName = "AbilityKit.Flow";

        /// <summary>
        /// Flow 包对象池配置模块名称。
        /// </summary>
        public const string ConfigModuleName = "AbilityKit.Flow.Pools";

        /// <summary>
        /// FlowContext 对象池键。
        /// </summary>
        public static readonly PoolKey ContextKey = new PoolKey("FlowContext");

        /// <summary>
        /// FlowContext 作用域字典的对象池键。
        /// </summary>
        public static readonly PoolKey ContextScopeMapKey = new PoolKey("FlowContext.ScopeMap");

        /// <summary>
        /// 阶段构建临时节点列表的对象池键。
        /// </summary>
        public static readonly PoolKey StageNodeListKey = new PoolKey("FlowStages.NodeList");

        /// <summary>
        /// FlowRunner 对象池键。
        /// </summary>
        public static readonly PoolKey RunnerKey = new PoolKey("FlowRunner");

        /// <summary>
        /// FlowSession 对象池键。
        /// </summary>
        public static readonly PoolKey SessionKey = new PoolKey("FlowSession");

        /// <summary>
        /// FlowHost 对象池键。
        /// </summary>
        public static readonly PoolKey HostKey = new PoolKey("FlowHost");

        /// <summary>
        /// FlowCompletion 对象池键。
        /// </summary>
        public static readonly PoolKey CompletionKey = new PoolKey("FlowCompletion");

        /// <summary>
        /// FlowEventQueue 对象池键。
        /// </summary>
        public static readonly PoolKey EventQueueKey = new PoolKey("FlowEventQueue");

        private static readonly object SyncRoot = new object();
        private static PoolConfigModule _defaultConfigModule;

        /// <summary>
        /// 注册 Flow 包默认对象池配置。项目侧可用更高优先级配置覆盖这些默认值。
        /// </summary>
        /// <returns>已注册的配置模块。</returns>
        public static PoolConfigModule RegisterDefaultConfig()
        {
            lock (SyncRoot)
            {
                if (_defaultConfigModule != null)
                {
                    return _defaultConfigModule;
                }

                _defaultConfigModule = Pools.RegisterConfigModule(
                    config => config
                        .Pool<FlowContext>(ScopeName, defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true, key: ContextKey)
                        .Pool<FlowRunner>(ScopeName, defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true, key: RunnerKey)
                        .Pool<FlowSession>(ScopeName, defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true, key: SessionKey)
                        .Pool<Dictionary<Type, object>>(ScopeName, defaultCapacity: 8, maxSize: 128, prewarmCount: 8, collectionCheck: true, key: ContextScopeMapKey)
                        .Pool<List<IFlowNode>>(ScopeName, defaultCapacity: 8, maxSize: 128, prewarmCount: 8, collectionCheck: true, key: StageNodeListKey)
                        .Pool<FlowCompletion>(ScopeName, defaultCapacity: 8, maxSize: 256, prewarmCount: 8, collectionCheck: true, key: CompletionKey),
                    defaultScopeName: ScopeName,
                    moduleName: ConfigModuleName,
                    source: "AbilityKit.Flow",
                    priority: 0);

                return _defaultConfigModule;
            }
        }

        /// <summary>
        /// 注册业务侧 Flow 对象池配置覆盖。业务包可以在启动阶段传入更高优先级配置，覆盖框架默认容量、裁剪策略或禁用指定池。
        /// </summary>
        /// <param name="configure">业务侧配置委托。</param>
        /// <param name="moduleName">业务配置模块名称。</param>
        /// <param name="source">业务配置来源。</param>
        /// <param name="priority">业务配置优先级；建议大于框架默认优先级 0。</param>
        /// <returns>可释放的配置注册句柄。</returns>
        public static PoolConfigRegistration RegisterOverride(
            Action<PoolConfigBuilder> configure,
            string moduleName,
            string source = null,
            int priority = 100)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new PoolConfigBuilder(ScopeName, moduleName, source, priority);
            configure(builder);
            var module = builder.Build();
            return Pools.RegisterConfigProvider(module, module.Info.Name, module.Info.Source, module.Info.Priority);
        }

        /// <summary>
        /// 为业务侧创建默认作用域为 Flow 的对象池配置构建器。
        /// </summary>
        public static PoolConfigBuilder CreateConfigBuilder(string moduleName, string source = null, int priority = 100)
        {
            return new PoolConfigBuilder(ScopeName, moduleName, source, priority);
        }

        /// <summary>
        /// 为指定参数类型的 FlowHost 注册对象池配置。FlowHost 是泛型类型，业务侧需要按实际 TArgs 分别配置。
        /// </summary>
        public static PoolConfigBuilder PoolHost<TArgs>(
            this PoolConfigBuilder builder,
            int defaultCapacity = 2,
            int maxSize = 32,
            int prewarmCount = 0,
            bool collectionCheck = true,
            PoolTrimPolicy trimPolicy = default,
            bool neverTrim = false)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            return builder.Pool<FlowHost<TArgs>>(
                ScopeName,
                defaultCapacity,
                maxSize,
                prewarmCount,
                collectionCheck,
                trimPolicy,
                neverTrim,
                HostKey);
        }

        /// <summary>
        /// 租借 FlowContext。
        /// </summary>
        public static FlowContext RentContext()
        {
            return Scope.Get(
                ContextKey,
                () => new FlowContext(),
                PoolItemConfig.Default(defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true),
                onGet: context => context.Clear());
        }

        /// <summary>
        /// 释放 FlowContext。
        /// </summary>
        public static void ReleaseContext(FlowContext context)
        {
            if (context == null) return;
            context.Clear();
            Scope.Release(ContextKey, context);
        }

        /// <summary>
        /// 租借 FlowRunner，并为其绑定一个池化 FlowContext。
        /// </summary>
        public static FlowRunner RentRunner()
        {
            return Scope.Get(
                RunnerKey,
                () => new FlowRunner(),
                PoolItemConfig.Default(defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true),
                onGet: runner => runner.ResetForRent(RentContext()));
        }

        /// <summary>
        /// 释放 FlowRunner，同时释放其绑定的 FlowContext。
        /// </summary>
        public static void ReleaseRunner(FlowRunner runner)
        {
            if (runner == null) return;
            var context = runner.ResetForRelease();
            ReleaseContext(context);
            Scope.Release(RunnerKey, runner);
        }

        /// <summary>
        /// 租借 FlowSession，并递归绑定池化 FlowRunner 与 FlowContext。
        /// </summary>
        public static FlowSession RentSession()
        {
            return Scope.Get(
                SessionKey,
                () => new FlowSession(deferRent: true),
                PoolItemConfig.Default(defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true),
                onGet: session => session.ResetForRent(RentRunner()),
                onRelease: session => session.ResetForRelease());
        }

        /// <summary>
        /// 释放 FlowSession，同时释放其绑定的 FlowRunner 与 FlowContext。
        /// </summary>
        public static void ReleaseSession(FlowSession session)
        {
            if (session == null) return;
            Scope.Release(SessionKey, session);
        }

        /// <summary>
        /// 租借 FlowHost，并递归绑定池化 FlowSession、FlowRunner 与 FlowContext。泛型 Host 的配置按具体 TArgs 类型区分。
        /// </summary>
        public static FlowHost<TArgs> RentHost<TArgs>(IFlowRootProvider<TArgs> provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            // provider 不能放进池的 onGet 闭包：Core 池只在首次创建时登记 options，
            // 后续调用传入的 onGet 会被丢弃，第一个 provider 会被闭包永久固化。
            // 改为 Get 之后显式绑定本次调用传入的 provider。
            var host = Scope.Get(
                HostKey,
                () => new FlowHost<TArgs>(deferRent: true),
                PoolItemConfig.Default(defaultCapacity: 2, maxSize: 32, prewarmCount: 0, collectionCheck: true),
                onRelease: host => host.ResetForRelease());
            host.ResetForRent(provider, RentSession());
            return host;
        }

        /// <summary>
        /// 释放 FlowHost，同时释放其绑定的 FlowSession、FlowRunner 与 FlowContext。
        /// </summary>
        public static void ReleaseHost<TArgs>(FlowHost<TArgs> host)
        {
            if (host == null) return;
            Scope.Release(HostKey, host);
        }

        /// <summary>
        /// 租借 FlowContext 作用域字典。
        /// </summary>
        public static Dictionary<Type, object> RentContextScopeMap()
        {
            return Scope.Get(
                ContextScopeMapKey,
                () => new Dictionary<Type, object>(),
                PoolItemConfig.Default(defaultCapacity: 8, maxSize: 128, prewarmCount: 8, collectionCheck: true));
        }

        /// <summary>
        /// 释放 FlowContext 作用域字典。
        /// </summary>
        public static void ReleaseContextScopeMap(Dictionary<Type, object> map)
        {
            if (map == null) return;
            map.Clear();
            Scope.Release(ContextScopeMapKey, map);
        }

        /// <summary>
        /// 租借阶段构建临时节点列表。
        /// </summary>
        public static List<IFlowNode> RentStageNodeList()
        {
            return Scope.Get(
                StageNodeListKey,
                () => new List<IFlowNode>(),
                PoolItemConfig.Default(defaultCapacity: 8, maxSize: 128, prewarmCount: 8, collectionCheck: true));
        }

        /// <summary>
        /// 释放阶段构建临时节点列表。
        /// </summary>
        public static void ReleaseStageNodeList(List<IFlowNode> nodes)
        {
            if (nodes == null) return;
            nodes.Clear();
            Scope.Release(StageNodeListKey, nodes);
        }

        /// <summary>
        /// 租借 FlowCompletion。
        /// </summary>
        public static FlowCompletion RentCompletion()
        {
            return Scope.Get(
                CompletionKey,
                () => new FlowCompletion(),
                PoolItemConfig.Default(defaultCapacity: 8, maxSize: 256, prewarmCount: 8, collectionCheck: true),
                onGet: completion => completion.Reset());
        }

        /// <summary>
        /// 释放 FlowCompletion。
        /// </summary>
        public static void ReleaseCompletion(FlowCompletion completion)
        {
            if (completion == null) return;
            completion.DetachWakeUp();
            completion.Reset();
            Scope.Release(CompletionKey, completion);
        }

        /// <summary>
        /// 租借 FlowEventQueue。泛型事件队列的配置按具体 TEvent 类型区分，业务侧可对实际事件类型注册高优先级配置。
        /// </summary>
        public static FlowEventQueue<TEvent> RentEventQueue<TEvent>()
        {
            return Scope.Get(
                EventQueueKey,
                () => new FlowEventQueue<TEvent>(),
                PoolItemConfig.Default(defaultCapacity: 2, maxSize: 64, prewarmCount: 2, collectionCheck: true));
        }

        /// <summary>
        /// 释放 FlowEventQueue。
        /// </summary>
        public static void ReleaseEventQueue<TEvent>(FlowEventQueue<TEvent> queue)
        {
            if (queue == null) return;
            queue.Clear();
            Scope.Release(EventQueueKey, queue);
        }

        private static PoolScope Scope => Pools.GetOrCreateScope(ScopeName, destroyOnDispose: false);
    }
}
