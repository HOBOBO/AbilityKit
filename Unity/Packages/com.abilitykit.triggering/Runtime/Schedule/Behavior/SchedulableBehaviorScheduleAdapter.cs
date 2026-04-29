using AbilityKit.Triggering.Runtime.Behavior;

namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 调度效果回调接口
    /// 用于监听调度生命周期事件
    /// </summary>
    public interface IScheduleEffectCallbacks
    {
        /// <summary>
        /// 调度完成时调用
        /// </summary>
        void OnCompleted(in ScheduleContext context);

        /// <summary>
        /// 调度被中断时调用
        /// </summary>
        void OnInterrupted(in ScheduleContext context, string reason);
    }

    /// <summary>
    /// 轻量级回调实现（仅提供 Action）
    /// </summary>
    public sealed class LightweightScheduleEffectCallbacks : IScheduleEffectCallbacks
    {
        private readonly System.Action<ScheduleContext> _onCompleted;
        private readonly System.Action<ScheduleContext, string> _onInterrupted;

        public LightweightScheduleEffectCallbacks(
            System.Action<ScheduleContext> onCompleted = null,
            System.Action<ScheduleContext, string> onInterrupted = null)
        {
            _onCompleted = onCompleted;
            _onInterrupted = onInterrupted;
        }

        public void OnCompleted(in ScheduleContext context)
        {
            _onCompleted?.Invoke(context);
        }

        public void OnInterrupted(in ScheduleContext context, string reason)
        {
            _onInterrupted?.Invoke(context, reason);
        }
    }

    /// <summary>
    /// SchedulableBehavior 调度效果适配器
    /// 将 ISchedulableBehavior 适配为 IScheduleEffect
    /// 实现通用调度管理中心（Level 4）与触发器调度层（Level 3）的衔接
    /// </summary>
    /// <remarks>
    /// 使用场景：
    /// - Buff 周期伤害（PeriodicTriggerBehavior）
    /// - 子弹飞行 + 碰撞（ParallelBehavior）
    /// - AOE 区域持续伤害（PeriodicTriggerBehavior）
    /// - 任何需要跨 Trigger 统一调度的行为
    /// </remarks>
    public sealed class SchedulableBehaviorScheduleAdapter : IScheduleEffect
    {
        private readonly ISchedulableBehavior _behavior;
        private readonly IBehaviorContext _context;
        private readonly IScheduleEffectCallbacks _callbacks;
        private readonly bool _beginOnFirstExecute;

        private bool _hasBegun;
        private bool _wasCompleted;
        private bool _wasInterrupted;

        /// <summary>
        /// 创建适配器
        /// </summary>
        /// <param name="behavior">可调度行为</param>
        /// <param name="context">行为上下文</param>
        /// <param name="callbacks">生命周期回调（可选）</param>
        /// <param name="beginOnFirstExecute">是否在首次执行时调用 Begin</param>
        public SchedulableBehaviorScheduleAdapter(
            ISchedulableBehavior behavior,
            IBehaviorContext context,
            IScheduleEffectCallbacks callbacks = null,
            bool beginOnFirstExecute = true)
        {
            _behavior = behavior ?? throw new System.ArgumentNullException(nameof(behavior));
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
            _callbacks = callbacks;
            _beginOnFirstExecute = beginOnFirstExecute;
        }

        /// <summary>
        /// 获取关联的行为
        /// </summary>
        public ISchedulableBehavior Behavior => _behavior;

        /// <summary>
        /// 获取行为上下文
        /// </summary>
        public IBehaviorContext Context => _context;

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public EBehaviorState State => _behavior.State;

        /// <summary>
        /// 是否已执行过
        /// </summary>
        public bool HasBegun => _hasBegun;

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool WasCompleted => _wasCompleted;

        /// <summary>
        /// 是否被中断
        /// </summary>
        public bool WasInterrupted => _wasInterrupted;

        /// <summary>
        /// 执行调度效果
        /// </summary>
        public void Execute(in ScheduleContext ctx)
        {
            // 首次执行时调用 Begin
            if (_beginOnFirstExecute && !_hasBegun)
            {
                _behavior.Begin(_context);
                _hasBegun = true;
            }

            // 跳过已完成或中断的行为
            if (_behavior.State == EBehaviorState.Completed)
            {
                if (!_wasCompleted)
                {
                    _wasCompleted = true;
                    _callbacks?.OnCompleted(ctx);
                }
                return;
            }

            if (_behavior.State == EBehaviorState.Interrupted)
            {
                if (!_wasInterrupted)
                {
                    _wasInterrupted = true;
                    _callbacks?.OnInterrupted(ctx, ctx.InterruptReason ?? "Unknown");
                }
                return;
            }

            // 创建适配后的上下文
            var adapterCtx = new ScheduleToBehaviorContextAdapter(ctx, _context);

            // 更新行为
            _behavior.Update(ctx.ScaledDeltaMs, adapterCtx);

            // 检查是否完成
            if (_behavior.State == EBehaviorState.Completed)
            {
                if (!_wasCompleted)
                {
                    _wasCompleted = true;
                    _callbacks?.OnCompleted(ctx);
                }
            }
            else if (_behavior.State == EBehaviorState.Interrupted)
            {
                if (!_wasInterrupted)
                {
                    _wasInterrupted = true;
                    _callbacks?.OnInterrupted(ctx, ctx.InterruptReason ?? "Unknown");
                }
            }
        }

        /// <summary>
        /// 执行前检查
        /// </summary>
        public bool CanExecute(in ScheduleContext ctx)
        {
            // 条件评估
            if (_behavior is ITriggerBehavior triggerBehavior)
            {
                var adapterCtx = new ScheduleToBehaviorContextAdapter(ctx, _context);
                if (!triggerBehavior.Evaluate(adapterCtx))
                    return false;
            }

            // 状态检查
            var state = _behavior.State;
            return state == EBehaviorState.Running 
                || state == EBehaviorState.Idle;
        }

        /// <summary>
        /// 手动开始行为
        /// </summary>
        public void Begin()
        {
            if (!_hasBegun)
            {
                _behavior.Begin(_context);
                _hasBegun = true;
            }
        }

        /// <summary>
        /// 手动中断行为
        /// </summary>
        public void Interrupt(string reason)
        {
            _behavior.Interrupt(reason);
        }

        /// <summary>
        /// 手动暂停行为
        /// </summary>
        public void Pause()
        {
            _behavior.Pause();
        }

        /// <summary>
        /// 手动恢复行为
        /// </summary>
        public void Resume()
        {
            _behavior.Resume();
        }

        /// <summary>
        /// 创建快照
        /// </summary>
        public BehaviorSnapshot CreateSnapshot()
        {
            return _behavior.CreateSnapshot();
        }

        /// <summary>
        /// 从快照恢复
        /// </summary>
        public void RestoreFromSnapshot(BehaviorSnapshot snapshot)
        {
            _behavior.RestoreFromSnapshot(snapshot);
        }
    }
}
