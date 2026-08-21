using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 领域服务解析器：节点通过它获取宿主服务（配置、目标搜索、时间源等），
    /// 使领域节点不依赖任何具体宿主装配。实现由接入方提供。
    /// </summary>
    public interface IBtServiceResolver
    {
        T Resolve<T>() where T : class;
        bool TryResolve<T>(out T service) where T : class;
    }

    /// <summary>按类型字典实现的默认服务解析器。</summary>
    public sealed class BtServiceResolver : IBtServiceResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public BtServiceResolver Add<T>(T service) where T : class
        {
            _services[typeof(T)] = service!;
            return this;
        }

        public T Resolve<T>() where T : class
        {
            if (!TryResolve<T>(out var service))
                throw new InvalidOperationException($"BT service '{typeof(T).Name}' is not registered.");
            return service;
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }
            service = null!;
            return false;
        }
    }

    /// <summary>
    /// 树级执行上下文：黑板 + 服务解析器 + 每 tick 刷新的帧号与定点时间。
    /// 由引擎持有并在节点生命周期回调中传入；节点可缓存引用（生命周期与树实例一致）。
    /// 构造器公开以支持宿主级单测直接驱动节点。
    /// </summary>
    public sealed class BtExecutionContext
    {
        public BtBlackboard Blackboard { get; }
        public IBtServiceResolver Services { get; }
        public int Frame { get; internal set; }
        public Fixed64 Time { get; internal set; }

        public BtExecutionContext(BtBlackboard blackboard, IBtServiceResolver services)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Services = services ?? new BtServiceResolver();
        }

        internal void BeginTick(int frame, Fixed64 time)
        {
            Frame = frame;
            Time = time;
        }
    }

    /// <summary>
    /// 节点初始化上下文：定义、类型化属性、子节点数、注册中心、专属随机流。
    /// 注意：不要用 init 访问器——Unity 编译环境（netstandard2.1）缺少 IsExternalInit。
    /// </summary>
    public struct BtNodeInitContext
    {
        public BtTreeDefinition Tree { get; set; }
        public BtNodeDefinition Definition { get; set; }
        public BtPropertyReader Properties { get; set; }
        public int ChildCount { get; set; }
        public BtNodeRegistry Registry { get; set; }
        /// <summary>从树种子与节点 id 派生的独立随机流；快照会捕获其完整状态。</summary>
        public DeterministicRandom Random { get; set; }
        public BtExecutionContext Context { get; set; }
    }

    /// <summary>
    /// 节点基类。生命周期：OnInit（建树一次）-> OnStart -> OnTick* -> OnStop。
    /// 禁止在节点内使用系统时间或非注入随机源（确定性硬约束）。
    /// </summary>
    public abstract class BtNodeBase
    {
        public string NodeId { get; internal set; } = "";
        public BtNodeState State { get; protected internal set; } = BtNodeState.Inactive;

        public virtual void OnInit(in BtNodeInitContext context) { }
        public virtual void OnStart(BtExecutionContext context) { }
        public virtual BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;
        public virtual void OnStop(BtExecutionContext context) { }
    }

    /// <summary>
    /// 有状态节点可实现此接口，把跨帧运行时状态（剩余时间、执行序、标记位等）
    /// 以确定性字符串负载纳入树快照。负载格式由节点自定，须可逆。
    /// </summary>
    public interface IBtNodeStateful
    {
        string CaptureState();
        void RestoreState(string payload);
    }

    /// <summary>条件节点基类：叶子，返回 Success / Failure，可参与组合节点的条件中断。</summary>
    public abstract class BtConditionNodeBase : BtNodeBase
    {
        public sealed override BtNodeState OnTick(BtExecutionContext context)
            => Validate(context) ? BtNodeState.Success : BtNodeState.Failure;

        protected abstract bool Validate(BtExecutionContext context);
    }

    /// <summary>动作节点基类：叶子，可返回 Running 跨帧持续。</summary>
    public abstract class BtActionNodeBase : BtNodeBase
    {
    }
}
