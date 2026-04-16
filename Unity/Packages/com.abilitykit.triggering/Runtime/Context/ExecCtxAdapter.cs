using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Variables.Numeric;
using BlackboardResolver = AbilityKit.Triggering.Runtime.Abstractions.IBlackboardResolver;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// ExecCtx 适配器：将 ExecCtx 中的服务适配为 ActionContext 可用的接口
    /// 作为 IServiceProvider 向 ActionContext 提供服务
    /// </summary>
    internal sealed class ExecCtxAdapter : IServiceProvider
    {
        private readonly ActionRegistry _actions;
        private readonly BlackboardResolver _blackboards;
        private readonly IPayloadAccessorRegistry _payloadRegistry;
        private readonly IEventBus _eventBus;
        private readonly INumericVarDomainRegistry _numericDomains;
        private readonly ExecPolicy _policy;

        // 懒加载的适配器
        private BlackboardResolver _blackboardResolver;
        private IPayloadAccessor _payloadAccessor;
        private IVariableRepository _variableRepository;
        private ITimeService _timeService;
        private IEntityFinder _entityFinder;

        public ExecCtxAdapter(
            ActionRegistry actions,
            BlackboardResolver blackboards,
            IPayloadAccessorRegistry payloadRegistry,
            IEventBus eventBus,
            INumericVarDomainRegistry numericDomains,
            ExecPolicy policy)
        {
            _actions = actions;
            _blackboards = blackboards;
            _payloadRegistry = payloadRegistry;
            _eventBus = eventBus;
            _numericDomains = numericDomains;
            _policy = policy;
        }

        // ========== IServiceProvider 实现 ==========
        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(BlackboardResolver)) return BlackboardResolver;
            if (serviceType == typeof(IPayloadAccessor)) return PayloadAccessor;
            if (serviceType == typeof(IVariableRepository)) return VariableRepository;
            if (serviceType == typeof(ITimeService)) return TimeService;
            if (serviceType == typeof(IEventBus)) return EventBus;
            if (serviceType == typeof(IEntityFinder)) return EntityFinder;
            if (serviceType == typeof(ActionRegistry)) return _actions;
            return null;
        }

        public T GetService<T>() where T : class => (T)GetService(typeof(T));

        // ========== 适配器属性（懒加载）===========
        public BlackboardResolver BlackboardResolver =>
            _blackboardResolver ??= new BlackboardResolverAdapter(_blackboards);

        public IPayloadAccessor PayloadAccessor =>
            _payloadAccessor ??= new PayloadAccessorAdapter(_payloadRegistry);

        public IVariableRepository VariableRepository =>
            _variableRepository ??= new VariableRepositoryAdapter(_numericDomains);

        public ITimeService TimeService =>
            _timeService ??= new TimeServiceAdapter(_policy);

        public IEventBus EventBus => _eventBus;

        public IEntityFinder EntityFinder =>
            _entityFinder ??= new EntityFinderAdapter();
    }

    /// <summary>
    /// 黑板解析器适配器
    /// </summary>
    internal sealed class BlackboardResolverAdapter : BlackboardResolver
    {
        private readonly BlackboardResolver _inner;
        public BlackboardResolverAdapter(BlackboardResolver inner) => _inner = inner;
        public bool TryResolve(int boardId, out IBlackboard board)
        {
            if (_inner != null)
                return _inner.TryResolve(boardId, out board);
            board = null;
            return false;
        }
        public IBlackboard GetOrCreate(int boardId) => _inner?.GetOrCreate(boardId);
    }

    /// <summary>
    /// 载荷访问器适配器
    /// </summary>
    internal sealed class PayloadAccessorAdapter : IPayloadAccessor
    {
        private readonly IPayloadAccessorRegistry _registry;
        public PayloadAccessorAdapter(IPayloadAccessorRegistry registry) => _registry = registry;
        public object Target => null;
        public bool TryGetPayloadDouble(in object args, int fieldId, out double value)
        {
            if (_registry != null)
                return _registry.TryGetDouble(in args, fieldId, out value);
            value = 0;
            return false;
        }
        public bool TryGetPayloadObject(in object args, int fieldId, out object value)
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// 变量仓库适配器
    /// </summary>
    internal sealed class VariableRepositoryAdapter : IVariableRepository
    {
        private readonly INumericVarDomainRegistry _domainRegistry;
        public VariableRepositoryAdapter(INumericVarDomainRegistry domainRegistry) => _domainRegistry = domainRegistry;
        public double GetNumeric(string domainId, string key)
        {
            if (_domainRegistry != null && _domainRegistry.TryGetDomain(domainId, out var domain))
            {
                // 使用默认空的 ExecCtx，因为 GetNumeric 没有泛型上下文
                if (domain.TryGet<object>(default, key, out var value))
                    return value;
            }
            return 0;
        }
        public void SetNumeric(string domainId, string key, double value) { }
        public bool Has(string domainId, string key) => _domainRegistry != null && _domainRegistry.TryGetDomain(domainId, out _);
    }

    /// <summary>
    /// 时间服务适配器
    /// </summary>
    internal sealed class TimeServiceAdapter : ITimeService
    {
        private readonly ExecPolicy _policy;
        public TimeServiceAdapter(ExecPolicy policy) => _policy = policy;
        public float DeltaTimeMs => _policy.DeltaTimeMs;
        public float TotalTimeMs => 0;
        public long CurrentTimestampMs => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// 实体查找器适配器（占位实现）
    /// </summary>
    internal sealed class EntityFinderAdapter : IEntityFinder
    {
        public T FindById<T>(int entityId) where T : class => null;
        public IEnumerable<T> FindByTag<T>(string tag) where T : class => Array.Empty<T>();
        public T FindNearest<T>(Abstractions.Vector3 position, float maxDistance = float.MaxValue) where T : class => null;
        public IEnumerable<T> FindInRange<T>(Abstractions.Vector3 center, float radius) where T : class => Array.Empty<T>();
    }
}
