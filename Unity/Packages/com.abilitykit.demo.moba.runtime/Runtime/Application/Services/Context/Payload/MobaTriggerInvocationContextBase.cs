namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 触发执行 payload 的统一契约。
    /// 用于把主动技能、被动触发、投射物命中、Buff 定时/阶段触发、伤害计算前后等触发时机收敛到同一套可溯源模型。
    /// </summary>
    public interface IMobaTriggerExecutionPayload : IMobaTriggerInvocationContext, IMobaTriggerLineageContextProvider, IMobaOriginContextProvider
    {
        MobaTriggerLineageContext LineageContext { get; }
        MobaGameplayOrigin Origin { get; }
    }

    /// <summary>
    /// 正式触发执行 payload 的推荐基类。
    /// 新的触发时机应继承该类型，以便在主动技能、被动、Buff、投射物、区域、召唤和伤害管线中保持统一的来源、链路与溯源能力。
    /// </summary>
    public abstract class MobaTriggerInvocationContextBase : IMobaTriggerExecutionPayload
    {
        public int TriggerId { get; set; }
        public abstract EffectContextKind Kind { get; }
        public int SourceActorId { get; set; }
        public int TargetActorId { get; set; }
        public long SourceContextId { get; set; }

        public virtual MobaTriggerLineageContext LineageContext
        {
            get
            {
                TryGetLineageContext(out var context);
                return context;
            }
        }

        public virtual MobaGameplayOrigin Origin
        {
            get
            {
                TryGetOrigin(out var origin);
                return origin;
            }
        }

        public abstract bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext);
        public abstract bool TryGetOrigin(out MobaGameplayOrigin origin);
    }
}
