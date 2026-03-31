namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 触发器表现层接口（与 IGameplayEffectCue 对称）
    /// 用于在触发器的各个生命周期节点播放 VFX / SFX / UI 反馈
    ///
    /// 设计原则：
    /// - 逻辑层（Predicate / Action）和表现层（Cue）完全分离
    /// - Cue 运行在帧同步客户端的渲染层，不参与逻辑判断
    /// - 与 Effect Cue 体系（IGameplayEffectCue）保持对称
    /// </summary>
    public interface ITriggerCue
    {
        /// <summary>
        /// 条件评估成功，进入 Execute 阶段前调用
        /// 典型用法：播放条件满足的视觉/音效反馈（如预警特效）
        /// </summary>
        void OnConditionPassed(in TriggerCueContext context);

        /// <summary>
        /// 条件评估失败，触发器跳过前调用
        /// 典型用法：播放"未命中"的表情、落空音效
        /// </summary>
        void OnConditionFailed(in TriggerCueContext context);

        /// <summary>
        /// 行为执行前调用（每个 Action 调用前都会触发）
        /// 典型用法：在技能释放前播放前摇特效
        /// </summary>
        void OnBeforeAction(in TriggerCueContext context, int actionIndex);

        /// <summary>
        /// 所有行为执行完成后调用
        /// 典型用法：播放打击特效、结算音效
        /// </summary>
        void OnExecuted(in TriggerCueContext context);

        /// <summary>
        /// 触发器被显式打断（ExecutionControl.StopPropagation/Cancel）时调用
        /// 典型用法：播放打断反馈（如"技能被中断"的提示音）
        /// </summary>
        void OnInterrupted(in TriggerCueContext context);

        /// <summary>
        /// 触发器因优先级机制被跳过（ShouldBlock 返回 true）时调用
        /// 典型用法：播放"被高优先级打断"的反馈
        /// </summary>
        void OnSkipped(in TriggerCueContext context);
    }
}
