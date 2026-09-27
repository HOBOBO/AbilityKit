namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 用于构建战斗执行上下文的统一来源数据。
    /// 避免在构建过程中再次回到 payload 提供者解析链路。
    /// </summary>
    public interface IMobaCombatContextSource
    {
        bool TryGetCombatContextSource(out MobaCombatContextSource source);
    }

    /// <summary>
    /// 用于构建战斗执行上下文的统一来源数据。
    /// 避免在构建过程中再次回到 payload 提供者解析链路。
    /// </summary>
    public readonly struct MobaCombatContextSource
    {
        public MobaCombatContextSource(
            EffectContextKind contextKind,
            MobaExecutionKind executionKind,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            int configId,
            int triggerId = 0,
            int frame = 0,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            string runtimeKind = null,
            int runtimeConfigId = 0,
            bool hasLiveRuntime = false)
        {
            ContextKind = contextKind;
            ExecutionKind = executionKind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            ConfigId = configId;
            TriggerId = triggerId;
            Frame = frame;
            SkillRuntimeHandle = skillRuntimeHandle;
            RuntimeKind = runtimeKind;
            RuntimeConfigId = runtimeConfigId;
            HasLiveRuntime = hasLiveRuntime;
        }

        /// <summary>归一化后的来源执行类型。</summary>
        public EffectContextKind ContextKind { get; }
        /// <summary>与来源数据关联的执行种类。</summary>
        public MobaExecutionKind ExecutionKind { get; }
        /// <summary>产生该来源数据的源角色。</summary>
        public int SourceActorId { get; }
        /// <summary>来源数据所指向的目标角色。</summary>
        public int TargetActorId { get; }
        /// <summary>用于继续向下游执行的来源上下文节点。</summary>
        public long SourceContextId { get; }
        /// <summary>来源链路中已知的根上下文。</summary>
        public long RootContextId { get; }
        /// <summary>来源链路中传递的所有权上下文标识。</summary>
        public long OwnerContextId { get; }
        /// <summary>来源数据对应的配置 ID。</summary>
        public int ConfigId { get; }
        /// <summary>当来源来自触发执行时，对应的触发器 ID。</summary>
        public int TriggerId { get; }
        /// <summary>用于运行时关联的帧号。</summary>
        public int Frame { get; }
        /// <summary>与来源关联的技能运行时句柄。</summary>
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        /// <summary>用于运行时诊断的运行时类型字符串。</summary>
        public string RuntimeKind { get; }
        /// <summary>用于运行时诊断的运行时配置 ID。</summary>
        public int RuntimeConfigId { get; }
        /// <summary>来源是否来自真实运行时实例。</summary>
        public bool HasLiveRuntime { get; }

        public bool IsValid => ContextKind != EffectContextKind.Unknown
                               || ExecutionKind != MobaExecutionKind.None
                               || SourceActorId != 0
                               || TargetActorId != 0
                               || SourceContextId != 0
                               || RootContextId != 0
                               || OwnerContextId != 0
                               || ConfigId != 0
                               || TriggerId != 0
                               || Frame != 0
                               || RuntimeConfigId != 0
                               || SkillRuntimeHandle.IsValid;
        public bool HasExecutionSource => SourceActorId > 0 && SourceContextId != 0;

        public MobaTriggerLineageContext ToLineageContext()
        {
            return new MobaTriggerLineageContext(
                ContextKind,
                ExecutionKind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                RootContextId != 0 ? RootContextId : SourceContextId,
                OwnerContextId,
                ConfigId);
        }

        public MobaTriggerExecutionSnapshot ToExecutionSnapshot()
        {
            return new MobaTriggerExecutionSnapshot(
                ContextKind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                RootContextId != 0 ? RootContextId : SourceContextId,
                OwnerContextId,
                TriggerId,
                ConfigId,
                Frame,
                SkillRuntimeHandle);
        }

        public MobaGameplayOrigin ToOrigin()
        {
            var lineageContext = ToLineageContext();
            var handle = SkillRuntimeHandle;
            return MobaGameplayOrigin.FromLineageContext(in lineageContext, in handle);
        }

        public MobaContextSourceView ToContextSourceView(MobaContextSourceResolveKind resolveKind, MobaContextSourceBoundary boundary)
        {
            return new MobaContextSourceView(
                resolveKind,
                boundary,
                ContextKind,
                ExecutionKind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                SourceContextId,
                RootContextId != 0 ? RootContextId : SourceContextId,
                OwnerContextId,
                ConfigId,
                TriggerId,
                Frame,
                RuntimeKind,
                RuntimeConfigId,
                HasLiveRuntime,
                SkillRuntimeHandle);
        }
    }

    public static class MobaCombatContextBuilder
    {
        public static MobaCombatExecutionContext FromSource(object payload, in MobaCombatContextSource source)
        {
            var lineageInput = source.ToLineageContext().ToLineageInput();
            var origin = source.ToOrigin();
            var snapshot = source.ToExecutionSnapshot();
            var facts = MobaCombatExecutionFacts.Resolve(payload);
            return new MobaCombatExecutionContext(payload, lineageInput, origin, snapshot, source.SkillRuntimeHandle, source.Frame, facts);
        }

        public static bool TryFromSource(object payload, out MobaCombatExecutionContext context)
        {
            context = default;
            if (payload is not IMobaCombatContextSource provider
                || !provider.TryGetCombatContextSource(out var source)
                || !source.HasExecutionSource)
            {
                return false;
            }
 
            context = FromSource(payload, in source);
            return context.HasExecutionSource;
        }

        public static bool TryFromSource(object payload, in MobaCombatContextSource source, out MobaCombatExecutionContext context)
        {
            context = default;
            if (!source.HasExecutionSource) return false;

            context = FromSource(payload, in source);
            return context.HasExecutionSource;
        }

        public static MobaCombatContextSource SkillCast(
            int skillId,
            int casterActorId,
            int targetActorId,
            long sourceContextId,
            int frame,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            return new MobaCombatContextSource(
                EffectContextKind.Skill,
                MobaExecutionKind.SkillCast,
                casterActorId,
                targetActorId,
                sourceContextId,
                sourceContextId,
                sourceContextId,
                skillId,
                triggerId: 0,
                frame: frame,
                skillRuntimeHandle: skillRuntimeHandle,
                runtimeKind: MobaRuntimeKindNames.SkillPipeline,
                runtimeConfigId: skillId,
                hasLiveRuntime: true);
        }
    }
}
