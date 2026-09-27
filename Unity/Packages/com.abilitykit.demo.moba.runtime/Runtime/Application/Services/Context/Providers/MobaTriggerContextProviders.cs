namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaTriggerLineageContextProvider
    {
        bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext);
    }

    public interface IMobaContextSourceProvider
    {
        bool TryGetContextSource(out MobaContextSourceView source);
    }

    public interface IMobaPersistentContextSourceProvider
    {
        bool TryGetPersistentContextSource(out MobaPersistentContextSourceSnapshot snapshot);
    }

    public enum MobaContextSourceBoundary
    {
        Unknown = 0,
        Snapshot = 1,
        Execution = 2,
        LiveRuntime = 3
    }

    public enum MobaContextSourceResolveKind
    {
        Unknown = 0,
        DirectProvider = 1,
        CombatExecutionContext = 2,
        Origin = 3,
        Lineage = 4,
        ExecutionSnapshot = 6,
        RuntimeDebug = 7
    }

    /// <summary>
    /// 已解析的上下文来源视图，用于查询、快照、保留、诊断和调试场景。
    /// 该模型有意覆盖较宽的来源信息，但不应替代 MobaCombatExecutionContext 作为执行期的正式模型。
    /// </summary>
    /// <remarks>
    /// SourceContextId 是提供者给出的来源/挂接节点；ParentContextId 是创建下游执行节点时使用的父节点；RootContextId 是已知的溯源链根节点；OwnerContextId 标识所有权上下文，新强类型上下文模型应优先使用该术语，而不是旧的 OwnerKey。
    /// </remarks>
    public readonly struct MobaContextSourceView
    {
        public MobaContextSourceView(
            MobaContextSourceResolveKind resolveKind,
            MobaContextSourceBoundary boundary,
            EffectContextKind contextKind,
            MobaExecutionKind executionKind,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            int configId,
            int triggerId,
            int frame,
            string runtimeKind,
            int runtimeConfigId,
            bool hasLiveRuntime,
            MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            ResolveKind = resolveKind;
            Boundary = boundary;
            ContextKind = contextKind;
            ExecutionKind = executionKind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            ConfigId = configId;
            TriggerId = triggerId;
            Frame = frame;
            RuntimeKind = runtimeKind;
            RuntimeConfigId = runtimeConfigId;
            HasLiveRuntime = hasLiveRuntime;
            SkillRuntimeHandle = skillRuntimeHandle;
        }

        /// <summary>该来源视图的解析方式。</summary>
        public MobaContextSourceResolveKind ResolveKind { get; }
        /// <summary>该来源视图代表的生命周期边界。</summary>
        public MobaContextSourceBoundary Boundary { get; }
        /// <summary>提供者能够判断时得到的归一化上下文类型。</summary>
        public EffectContextKind ContextKind { get; }
        /// <summary>与来源视图关联的执行种类。</summary>
        public MobaExecutionKind ExecutionKind { get; }
        /// <summary>产生来源上下文的角色。</summary>
        public int SourceActorId { get; }
        /// <summary>来源上下文指向的目标角色。</summary>
        public int TargetActorId { get; }
        /// <summary>提供者给出的来源/挂接节点；对链路和快照而言，通常会成为下游父上下文。</summary>
        public long SourceContextId { get; }
        /// <summary>创建下游执行节点时使用的父节点。</summary>
        public long ParentContextId { get; }
        /// <summary>溯源链中已知的根节点。</summary>
        public long RootContextId { get; }
        /// <summary>由强类型上下文模型传递的所有权上下文标识。</summary>
        public long OwnerContextId { get; }
        /// <summary>该来源代表的配置 ID。</summary>
        public int ConfigId { get; }
        /// <summary>该来源代表的触发器 ID；没有触发器时为 0。</summary>
        public int TriggerId { get; }
        /// <summary>该来源代表的帧号；不可用时为 0。</summary>
        public int Frame { get; }
        /// <summary>用于实时运行时诊断的运行时类型字符串，不应作为业务来源分类的主依据。</summary>
        public string RuntimeKind { get; }
        /// <summary>用于实时运行时诊断的运行时配置 ID；正式业务判断应优先使用 ConfigId。</summary>
        public int RuntimeConfigId { get; }
        /// <summary>该视图是否来自实时运行时实例；这是诊断/生命周期信息，不是快照真值。</summary>
        public bool HasLiveRuntime { get; }
        /// <summary>与来源关联的技能运行时句柄；不可用时为空句柄。</summary>
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        public bool IsValid => ContextKind != EffectContextKind.Unknown || SourceActorId != 0 || TargetActorId != 0 || SourceContextId != 0 || ParentContextId != 0 || RootContextId != 0 || OwnerContextId != 0 || ConfigId != 0 || TriggerId != 0 || Frame != 0 || RuntimeConfigId != 0 || SkillRuntimeHandle.IsValid;
        public bool HasExecutionSource => SourceActorId > 0 && SourceContextId != 0;
        public bool IsFormalSource => Boundary == MobaContextSourceBoundary.Execution || Boundary == MobaContextSourceBoundary.Snapshot;
        public bool IsDiagnosticSource => ResolveKind == MobaContextSourceResolveKind.RuntimeDebug || Boundary == MobaContextSourceBoundary.LiveRuntime || HasLiveRuntime;
        public bool HasRuntimeDiagnostics => HasLiveRuntime || RuntimeConfigId != 0 || !string.IsNullOrEmpty(RuntimeKind);

        public static MobaContextSourceView FromOrigin(in MobaGameplayOrigin origin, MobaContextSourceResolveKind resolveKind = MobaContextSourceResolveKind.Origin, MobaContextSourceBoundary boundary = MobaContextSourceBoundary.Snapshot, bool hasLiveRuntime = false, string runtimeKind = null, int runtimeConfigId = 0)
        {
            return new MobaContextSourceView(
                resolveKind,
                boundary,
                EffectContextKind.Unknown,
                origin.ImmediateKind,
                origin.SourceActorId,
                origin.TargetActorId,
                origin.ImmediateContextId,
                origin.EffectiveParentContextId,
                origin.EffectiveRootContextId,
                origin.OwnerContextId,
                origin.ImmediateConfigId,
                0,
                0,
                runtimeKind,
                runtimeConfigId,
                hasLiveRuntime,
                origin.SkillRuntimeHandle);
        }

        public static MobaContextSourceView FromLineage(in MobaTriggerLineageContext lineageContext, MobaContextSourceResolveKind resolveKind = MobaContextSourceResolveKind.Lineage, MobaContextSourceBoundary boundary = MobaContextSourceBoundary.Snapshot, MobaSkillCastRuntimeHandle skillRuntimeHandle = default, bool hasLiveRuntime = false, string runtimeKind = null, int runtimeConfigId = 0)
        {
            return new MobaContextSourceView(
                resolveKind,
                boundary,
                lineageContext.ContextKind,
                lineageContext.OriginKind,
                lineageContext.SourceActorId,
                lineageContext.TargetActorId,
                lineageContext.SourceContextId,
                lineageContext.SourceContextId,
                lineageContext.RootContextId != 0 ? lineageContext.RootContextId : lineageContext.SourceContextId,
                lineageContext.OwnerContextId,
                lineageContext.SourceConfigId,
                0,
                0,
                runtimeKind,
                runtimeConfigId,
                hasLiveRuntime,
                skillRuntimeHandle);
        }

        public static MobaContextSourceView FromExecutionSnapshot(in MobaTriggerExecutionSnapshot snapshot, MobaContextSourceResolveKind resolveKind = MobaContextSourceResolveKind.ExecutionSnapshot)
        {
            return new MobaContextSourceView(
                resolveKind,
                MobaContextSourceBoundary.Execution,
                snapshot.Kind,
                MobaExecutionKind.EffectExecution,
                snapshot.SourceActorId,
                snapshot.TargetActorId,
                snapshot.SourceContextId,
                snapshot.SourceContextId,
                snapshot.EffectiveRootContextId,
                snapshot.OwnerContextId,
                snapshot.ConfigId,
                snapshot.TriggerId,
                snapshot.Frame,
                null,
                0,
                false,
                snapshot.SkillRuntimeHandle);
        }

        public static MobaContextSourceView FromRuntimeDebug(in MobaContinuousRuntimeDebugInfo debug)
        {
            return new MobaContextSourceView(
                MobaContextSourceResolveKind.RuntimeDebug,
                MobaContextSourceBoundary.LiveRuntime,
                EffectContextKind.Unknown,
                MobaExecutionKind.None,
                debug.SourceActorId,
                debug.TargetActorId,
                debug.SourceContextId,
                debug.ParentContextId,
                debug.RootContextId,
                debug.OwnerContextId,
                debug.ConfigId,
                0,
                0,
                debug.Kind,
                debug.ConfigId,
                true,
                debug.SkillRuntimeHandle);
        }
    }
}
