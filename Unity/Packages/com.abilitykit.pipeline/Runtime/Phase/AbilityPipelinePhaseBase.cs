using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线阶段基类
    /// 统一模型：所有阶段都有 IsComplete 状态
    /// - 瞬时阶段：在 OnExecute 中设置 IsComplete = true
    /// - 持续阶段：在 OnUpdate 中根据条件设置 IsComplete = true
    /// </summary>
    public abstract class AbilityPipelinePhaseBase<TCtx> : IAbilityPipelinePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 阶段ID
        /// </summary>
        public AbilityPipelinePhaseId PhaseId { get; protected set; }
        
        /// <summary>
        /// 是否已完成
        /// </summary>
        public virtual bool IsComplete { get; protected set; }
        
        /// <summary>
        /// 是否是复合阶段
        /// </summary>
        public virtual bool IsComposite => false;
        
        /// <summary>
        /// 子阶段列表（非复合阶段为空）
        /// </summary>
        public virtual IReadOnlyList<IAbilityPipelinePhase<TCtx>> SubPhases => null;
        
        /// <summary>
        /// 阶段名称（用于调试）
        /// </summary>
        public string PhaseName { get; set; }

        protected AbilityPipelinePhaseBase(AbilityPipelinePhaseId phaseId)
        {
            PhaseId = phaseId;
        }

        protected AbilityPipelinePhaseBase(string phaseName)
        {
            PhaseId = new AbilityPipelinePhaseId(phaseName);
            PhaseName = phaseName;
        }

        /// <summary>
        /// 判断是否应执行该阶段
        /// </summary>
        public virtual bool ShouldExecute(TCtx context)
        {
            return true;
        }

        /// <summary>
        /// 执行阶段逻辑
        /// </summary>
        public void Execute(TCtx context)
        {
            IsComplete = false;
            OnEnter(context);
            OnExecute(context);
            // 注意：瞬时阶段应在 OnExecute 中设置 IsComplete = true
        }

        /// <summary>
        /// 每帧更新（持续阶段需要重写）
        /// </summary>
        public virtual void OnUpdate(TCtx context, float deltaTime)
        {
            // 基类默认什么都不做（瞬时阶段不需要Update）
        }

        /// <summary>
        /// 重置阶段状态
        /// </summary>
        public virtual void Reset()
        {
            IsComplete = false;
        }

        /// <summary>
        /// 进入阶段时调用
        /// </summary>
        protected virtual void OnEnter(TCtx context) { }

        /// <summary>
        /// 执行阶段核心逻辑
        /// </summary>
        protected abstract void OnExecute(TCtx context);

        /// <summary>
        /// 退出阶段时调用
        /// </summary>
        protected virtual void OnExit(TCtx context) { }

        /// <summary>
        /// 完成阶段
        /// </summary>
        protected virtual void Complete(TCtx context)
        {
            if (IsComplete) return;
            IsComplete = true;
            OnExit(context);
        }

        /// <summary>
        /// 处理阶段执行中的错误
        /// </summary>
        public virtual void HandleError(TCtx context, Exception exception)
        {
            //LogUtil.LogError($"[AbilityPipeline] Phase '{PhaseName ?? PhaseId.ToString()}' error: {exception.Message}");
        }
    }

    /// <summary>
    /// 瞬时阶段基类
    /// Execute 后立即完成
    /// </summary>
    public abstract class AbilityInstantPhaseBase<TCtx> : AbilityPipelinePhaseBase<TCtx>, IAbilityInstantPhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        protected AbilityInstantPhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }
        protected AbilityInstantPhaseBase(string phaseName) : base(phaseName) { }

        /// <summary>
        /// 执行瞬时逻辑并立即完成
        /// </summary>
        protected sealed override void OnExecute(TCtx context)
        {
            OnInstantExecute(context);
            Complete(context); // 立即完成
        }

        /// <summary>
        /// 瞬时执行逻辑（子类实现）
        /// </summary>
        protected abstract void OnInstantExecute(TCtx context);
    }

    /// <summary>
    /// 持续性阶段基类
    /// 需要 OnUpdate 驱动，在满足条件时完成
    /// </summary>
    public abstract class AbilityDurationalPhaseBase<TCtx> : AbilityPipelinePhaseBase<TCtx>, IDurationalPhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 阶段持续时间（-1表示无限，0表示瞬时）
        /// </summary>
        public float Duration { get; set; } = -1f;
        
        /// <summary>
        /// 当前已运行时间
        /// </summary>
        protected float _elapsedTime;

        protected AbilityDurationalPhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }
        protected AbilityDurationalPhaseBase(string phaseName) : base(phaseName) { }

        /// <summary>
        /// 进入阶段
        /// </summary>
        protected override void OnEnter(TCtx context)
        {
            base.OnEnter(context);
            _elapsedTime = 0f;
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public override void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete || context.IsPaused)
                return;

            _elapsedTime += deltaTime;
            
            OnTick(context, deltaTime);
            
            // 检查时间是否到达
            if (Duration > 0 && _elapsedTime >= Duration)
            {
                Complete(context);
            }
        }

        /// <summary>
        /// 每帧更新逻辑（子类重写）
        /// </summary>
        protected virtual void OnTick(TCtx context, float deltaTime) { }

        /// <summary>
        /// 强制完成
        /// </summary>
        public void ForceComplete(TCtx context)
        {
            Complete(context);
        }

        /// <summary>
        /// 重置状态
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _elapsedTime = 0f;
        }
    }

    /// <summary>
    /// 可中断的持续性阶段基类
    /// </summary>
    public abstract class AbilityInterruptiblePhaseBase<TCtx> : AbilityDurationalPhaseBase<TCtx>, IInterruptiblePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        protected AbilityInterruptiblePhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }
        protected AbilityInterruptiblePhaseBase(string phaseName) : base(phaseName) { }

        /// <summary>
        /// 中断处理
        /// </summary>
        public virtual void OnInterrupt(TCtx context)
        {
            IsComplete = true;
            OnExit(context);
        }
    }
}

