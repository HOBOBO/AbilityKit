namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 触发提供者传播的统一链路上下文。
    /// 在转换为效果执行输入或溯源快照之前，会先通过该结构承载链路信息。
    /// </summary>
    public readonly struct MobaTriggerLineageContext
    {
        public MobaTriggerLineageContext(
            EffectContextKind contextKind,
            MobaExecutionKind originKind,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            int sourceConfigId)
        {
            ContextKind = contextKind;
            OriginKind = originKind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SourceConfigId = sourceConfigId;
        }

        public EffectContextKind ContextKind { get; }
        public MobaExecutionKind OriginKind { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        /// <summary>
        /// 下游触发/效果执行使用的来源溯源节点。通常会作为下一个执行节点的父节点。
        /// </summary>
        public long SourceContextId { get; }
        /// <summary>
        /// 该链路已知的根溯源节点。为 0 时，转换器会将 SourceContextId 作为有效根节点。
        /// </summary>
        public long RootContextId { get; }
        /// <summary>该链路传递的所有权上下文标识。</summary>
        public long OwnerContextId { get; }
 
        /// <summary>所有权上下文标识的兼容别名。</summary>
        public long OwnerKey => OwnerContextId;
        public int SourceConfigId { get; }

        public bool HasExecutionSource => SourceActorId > 0 && SourceContextId != 0;

        public MobaEffectLineageInput ToLineageInput()
        {
            return new MobaEffectLineageInput(
                ContextKind,
                OriginKind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                RootContextId,
                OwnerContextId,
                SourceConfigId);
        }

    }
}
