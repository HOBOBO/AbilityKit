using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线阶段基类。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public abstract class AbilityPipelinePhaseBase<TCtx> : IAbilityPipelinePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 阶段 ID。
        /// </summary>
        public AbilityPipelinePhaseId PhaseId { get; protected set; }
        
        /// <summary>
        /// 是否已完成。
        /// </summary>
        public virtual bool IsComplete { get; protected set; }
        
        /// <summary>
        /// 是否为复合阶段。
        /// </summary>
        public virtual bool IsComposite => false;
        
        /// <summary>
        /// 子阶段列表；非复合阶段返回空列表。
        /// </summary>
        public virtual IReadOnlyList<IAbilityPipelinePhase<TCtx>> SubPhases => Array.Empty<IAbilityPipelinePhase<TCtx>>();
        
        /// <summary>
        /// 阶段名称，用于调试与追踪。
        /// </summary>
        public string PhaseName { get; set; }

        /// <summary>
        /// 使用阶段 ID 创建阶段。
        /// </summary>
        /// <param name="phaseId">阶段 ID。</param>
        protected AbilityPipelinePhaseBase(AbilityPipelinePhaseId phaseId)
        {
            PhaseId = phaseId;
            PhaseName = phaseId.ToString();
        }

        /// <summary>
        /// 使用阶段名称创建阶段。
        /// </summary>
        /// <param name="phaseName">阶段名称。</param>
        protected AbilityPipelinePhaseBase(string phaseName)
        {
            PhaseId = new AbilityPipelinePhaseId(phaseName);
            PhaseName = phaseName;
        }

        /// <summary>
        /// 判断当前阶段是否应执行。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        /// <returns>如果应执行则返回 true。</returns>
        public virtual bool ShouldExecute(TCtx context)
        {
            return true;
        }

        /// <summary>
        /// 执行阶段逻辑。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        public void Execute(TCtx context)
        {
            IsComplete = false;
            OnEnter(context);
            OnExecute(context);
        }

        /// <summary>
        /// 每帧更新阶段。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        /// <param name="deltaTime">本帧间隔时间。</param>
        public virtual void OnUpdate(TCtx context, float deltaTime)
        {
        }

        /// <summary>
        /// 重置阶段运行状态。
        /// </summary>
        public virtual void Reset()
        {
            IsComplete = false;
        }

        public void SetComplete(bool isComplete) => IsComplete = isComplete;

        /// <summary>
        /// 阶段进入时调用。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        protected virtual void OnEnter(TCtx context) { }

        /// <summary>
        /// 执行阶段核心逻辑。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        protected abstract void OnExecute(TCtx context);

        /// <summary>
        /// 阶段退出时调用。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        protected virtual void OnExit(TCtx context) { }

        /// <summary>
        /// 标记阶段完成并触发退出回调。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        protected virtual void Complete(TCtx context)
        {
            if (IsComplete) return;
            IsComplete = true;
            OnExit(context);
        }

        /// <summary>
        /// 处理阶段执行错误。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        /// <param name="exception">执行异常。</param>
        public virtual void HandleError(TCtx context, Exception exception)
        {
        }
    }

    /// <summary>
    /// 瞬时阶段基类，执行后立即完成。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public abstract class AbilityInstantPhaseBase<TCtx> : AbilityPipelinePhaseBase<TCtx>, IAbilityInstantPhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 使用阶段 ID 创建瞬时阶段。
        /// </summary>
        /// <param name="phaseId">阶段 ID。</param>
        protected AbilityInstantPhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }

        /// <summary>
        /// 使用阶段名称创建瞬时阶段。
        /// </summary>
        /// <param name="phaseName">阶段名称。</param>
        protected AbilityInstantPhaseBase(string phaseName) : base(phaseName) { }

        /// <summary>
        /// 执行瞬时逻辑并立即完成阶段。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        protected sealed override void OnExecute(TCtx context)
        {
            OnInstantExecute(context);
            Complete(context);
        }

        /// <summary>
        /// 瞬时执行逻辑。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        protected abstract void OnInstantExecute(TCtx context);
    }

    /// <summary>
    /// 持续性阶段基类，由 OnUpdate 驱动完成。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public abstract class AbilityDurationalPhaseBase<TCtx> : AbilityPipelinePhaseBase<TCtx>, IDurationalPhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 阶段持续时间；小于 0 表示无限持续，0 表示由阶段自身立即判定。
        /// </summary>
        public float Duration { get; set; } = -1f;
        
        /// <summary>
        /// 当前已运行时间。
        /// </summary>
        protected float _elapsedTime;

        public float ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _elapsedTime = value;
            }
        }

        /// <summary>
        /// 使用阶段 ID 创建持续阶段。
        /// </summary>
        /// <param name="phaseId">阶段 ID。</param>
        protected AbilityDurationalPhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }

        /// <summary>
        /// 使用阶段名称创建持续阶段。
        /// </summary>
        /// <param name="phaseName">阶段名称。</param>
        protected AbilityDurationalPhaseBase(string phaseName) : base(phaseName) { }

        /// <inheritdoc />
        protected override void OnEnter(TCtx context)
        {
            base.OnEnter(context);
            _elapsedTime = 0f;
        }

        /// <inheritdoc />
        public override void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete || context.IsPaused)
                return;

            _elapsedTime += deltaTime;
            OnTick(context, deltaTime);
            
            if (Duration > 0 && _elapsedTime >= Duration)
            {
                Complete(context);
            }
        }

        /// <summary>
        /// 每帧推进逻辑。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        /// <param name="deltaTime">本帧间隔时间。</param>
        protected virtual void OnTick(TCtx context, float deltaTime) { }

        /// <summary>
        /// 强制完成阶段。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        public void ForceComplete(TCtx context)
        {
            Complete(context);
        }

        /// <inheritdoc />
        public override void Reset()
        {
            base.Reset();
            _elapsedTime = 0f;
        }

    }

    /// <summary>
    /// 可中断的持续性阶段基类。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public abstract class AbilityInterruptiblePhaseBase<TCtx> : AbilityDurationalPhaseBase<TCtx>, IInterruptiblePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 使用阶段 ID 创建可中断持续阶段。
        /// </summary>
        /// <param name="phaseId">阶段 ID。</param>
        protected AbilityInterruptiblePhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }

        /// <summary>
        /// 使用阶段名称创建可中断持续阶段。
        /// </summary>
        /// <param name="phaseName">阶段名称。</param>
        protected AbilityInterruptiblePhaseBase(string phaseName) : base(phaseName) { }

        /// <summary>
        /// 中断当前阶段。
        /// </summary>
        /// <param name="context">管线上下文。</param>
        public virtual void OnInterrupt(TCtx context)
        {
            IsComplete = true;
            OnExit(context);
        }
    }
}
