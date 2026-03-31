namespace AbilityKit.Triggering.Runtime
{
    public interface ITrigger<TArgs, TCtx>
    {
        bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx);
        void Execute(in TArgs args, in ExecCtx<TCtx> ctx);

        /// <summary>
        /// 触发器的表现层 Cue（VFX / SFX / UI）
        /// 默认返回 NullTriggerCue.Instance
        /// </summary>
        ITriggerCue Cue => NullTriggerCue.Instance;
    }

    /// <summary>
    /// 携带 TriggerId 的触发器接口（用于 Cue 溯源）
    /// </summary>
    public interface ITriggerWithId
    {
        int TriggerId { get; }
    }
}
