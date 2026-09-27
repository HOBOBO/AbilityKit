namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaOriginContextProvider
    {
        bool TryGetOrigin(out MobaGameplayOrigin origin);
    }

    /// <summary>
    /// 仅用于归因的玩法来源模型。
    /// 记录即时来源事件及其派生出的有效链路边界。
    /// </summary>
    public readonly struct MobaGameplayOrigin
    {
        public MobaGameplayOrigin(
            int sourceActorId,
            int targetActorId,
            MobaExecutionKind immediateKind,
            int immediateConfigId,
            long immediateContextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ImmediateKind = immediateKind;
            ImmediateConfigId = immediateConfigId;
            ImmediateContextId = immediateContextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
        }

        /// <summary>产生来源事件的角色。</summary>
        public int SourceActorId { get; }
        /// <summary>来源事件指向的目标角色。</summary>
        public int TargetActorId { get; }
        /// <summary>产生该来源的即时溯源种类。</summary>
        public MobaExecutionKind ImmediateKind { get; }
        /// <summary>即时来源事件对应的配置 ID。</summary>
        public int ImmediateConfigId { get; }
        /// <summary>事件本身对应的即时溯源节点。</summary>
        public long ImmediateContextId { get; }
        /// <summary>即时节点挂接到更大链路时使用的父上下文。</summary>
        public long ParentContextId { get; }
        /// <summary>本来源携带的有效根上下文。</summary>
        public long RootContextId { get; }
        /// <summary>在来源链路中传递的所有权上下文标识。</summary>
        public long OwnerContextId { get; }
        /// <summary>与来源关联的技能运行时句柄（如有）。</summary>
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }

        public bool IsValid => SourceActorId > 0 || TargetActorId > 0 || ParentContextId != 0 || ImmediateContextId != 0 || RootContextId != 0 || OwnerContextId != 0 || SkillRuntimeHandle.IsValid;
        /// <summary>是否具备可继续创建下游执行节点的来源信息。</summary>
        public bool HasExecutionSource => SourceActorId > 0 && EffectiveParentContextId != 0;
        /// <summary>优先返回父上下文，缺失时回退到即时节点。</summary>
        public long EffectiveParentContextId => ParentContextId != 0 ? ParentContextId : ImmediateContextId;
        /// <summary>优先返回根上下文，缺失时回退到有效父上下文。</summary>
        public long EffectiveRootContextId => RootContextId != 0 ? RootContextId : EffectiveParentContextId;

        public MobaTriggerLineageContext ToLineageContext(EffectContextKind contextKind)
        {
            return new MobaTriggerLineageContext(
                contextKind,
                ImmediateKind != MobaExecutionKind.None ? ImmediateKind : MobaExecutionKind.EffectExecution,
                SourceActorId,
                TargetActorId,
                EffectiveParentContextId,
                EffectiveRootContextId,
                OwnerContextId,
                ImmediateConfigId);
        }

        public MobaGameplayOrigin WithImmediate(MobaExecutionKind kind, int configId, long contextId, long ownerContextId = 0)
        {
            return MobaGameplayOriginBuilder.Create()
                .FromOrigin(in this)
                .WithImmediate(kind, configId, contextId)
                .WithOwnerContext(ownerContextId != 0 ? ownerContextId : OwnerContextId)
                .Build();
        }

        public MobaGameplayOrigin WithActors(int sourceActorId, int targetActorId)
        {
            return MobaGameplayOriginBuilder.Create()
                .FromOrigin(in this)
                .WithActors(sourceActorId, targetActorId)
                .Build();
        }

        public MobaGameplayOrigin WithSkillRuntime(in MobaSkillCastRuntimeHandle handle)
        {
            return MobaGameplayOriginBuilder.Create()
                .FromOrigin(in this)
                .WithSkillRuntime(in handle)
                .Build();
        }

        public static MobaGameplayOrigin FromLineageContext(in MobaTriggerLineageContext lineageContext, in MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            return MobaGameplayOriginBuilder.Create()
                .FromLineageContext(in lineageContext)
                .WithSkillRuntime(in skillRuntimeHandle)
                .Build();
        }

    }
}
