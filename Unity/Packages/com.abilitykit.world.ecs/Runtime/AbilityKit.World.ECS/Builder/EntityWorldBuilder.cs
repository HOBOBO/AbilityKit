using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// EntityWorld 构建器。
    /// 提供流畅的配置 API。
    /// </summary>
    public class EntityWorldBuilder
    {
        private int _initialCapacity = 64;
        private int _maxCapacity = int.MaxValue;
        private IComponentRegistry _componentRegistry;
        private IWorldEventBus _eventBus;
        private Action<string> _logHandler;

        /// <summary>
        /// 设置初始容量（默认 64）。
        /// </summary>
        public EntityWorldBuilder WithInitialCapacity(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _initialCapacity = capacity;
            return this;
        }

        /// <summary>
        /// 设置最大容量（默认 int.MaxValue）。
        /// </summary>
        public EntityWorldBuilder WithMaxCapacity(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _maxCapacity = capacity;
            return this;
        }

        /// <summary>
        /// 注入自定义组件注册表。
        /// </summary>
        public EntityWorldBuilder WithComponentRegistry(IComponentRegistry registry)
        {
            _componentRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            return this;
        }

        /// <summary>
        /// 注入自定义事件总线。
        /// </summary>
        public EntityWorldBuilder WithEventBus(IWorldEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            return this;
        }

        /// <summary>
        /// 设置日志处理器。
        /// </summary>
        public EntityWorldBuilder WithLogger(Action<string> logHandler)
        {
            _logHandler = logHandler ?? throw new ArgumentNullException(nameof(logHandler));
            return this;
        }

        /// <summary>
        /// 构建 EntityWorld 实例。
        /// </summary>
        public IECWorld Build()
        {
            return new EntityWorld(
                initialCapacity: _initialCapacity,
                maxCapacity: _maxCapacity,
                componentRegistry: _componentRegistry,
                eventBus: _eventBus,
                logHandler: _logHandler
            );
        }

        /// <summary>
        /// 构建 EntityWorld 实例（返回具体类型）。
        /// </summary>
        public EntityWorld BuildTyped()
        {
            return new EntityWorld(
                initialCapacity: _initialCapacity,
                maxCapacity: _maxCapacity,
                componentRegistry: _componentRegistry,
                eventBus: _eventBus,
                logHandler: _logHandler
            );
        }
    }

    /// <summary>
    /// 快捷扩展方法。
    /// </summary>
    public static class WorldBuilderExtensions
    {
        /// <summary>
        /// 创建 EntityWorld 构建器。
        /// </summary>
        public static EntityWorldBuilder CreateWorldBuilder()
        {
            return new EntityWorldBuilder();
        }

        /// <summary>
        /// 快捷创建 EntityWorld。
        /// </summary>
        public static IECWorld CreateWorld(
            int initialCapacity = 64,
            int maxCapacity = int.MaxValue)
        {
            return new EntityWorld(initialCapacity, maxCapacity);
        }
    }
}
