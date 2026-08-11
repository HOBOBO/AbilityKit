using AbilityKit.Context;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaCombatExecutionContextProvider
    {
        bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context);
    }

    /// <summary>
    /// MOBA 战斗执行期的统一上下文。
    /// 业务执行代码应先把触发 payload 归一化到该模型，再读取溯源、链路、快照、技能运行时和帧信息。
    /// </summary>
    public readonly struct MobaCombatExecutionContext : IMobaContextSourceProvider, IMobaRuntimeContextPayload
    {
        public MobaCombatExecutionContext(
            object payload,
            MobaEffectLineageInput lineageInput,
            MobaGameplayOrigin origin,
            MobaTriggerExecutionSnapshot executionSnapshot,
            MobaSkillCastRuntimeHandle skillRuntimeHandle,
            int frame)
        {
            Payload = payload;
            LineageInput = lineageInput;
            Origin = origin;
            ExecutionSnapshot = executionSnapshot;
            SkillRuntimeHandle = skillRuntimeHandle;
            Frame = frame != 0 ? frame : executionSnapshot.Frame;
        }

        /// <summary>
        /// 归一化到链路、来源和快照之前的原始触发 payload。
        /// 优先使用本上下文的强类型访问器，业务代码仅应在桥接或解析边界读取原始 payload。
        /// </summary>
        public object Payload { get; }
        public string PayloadTypeName => Payload != null ? Payload.GetType().Name : null;
        public bool HasOriginalPayload => Payload != null;
        /// <summary>决定当前执行如何挂接到溯源链路的链路输入。</summary>
        public MobaEffectLineageInput LineageInput { get; }
        /// <summary>导致本次执行的即时玩法事件来源。</summary>
        public MobaGameplayOrigin Origin { get; }
        /// <summary>记录执行瞬间已归一化的运行态快照。</summary>
        public MobaTriggerExecutionSnapshot ExecutionSnapshot { get; }
        /// <summary>与本次执行关联的技能运行时句柄（如有）。</summary>
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        /// <summary>用于溯源与调试关联的帧号。</summary>
        public int Frame { get; }
 
        /// <summary>先从链路输入、再从快照推导出的执行类型。</summary>
        public EffectContextKind ContextKind => LineageInput.ContextKind != EffectContextKind.Unknown ? LineageInput.ContextKind : ExecutionSnapshot.Kind;
        /// <summary>从链路层继承的溯源种类。</summary>
        public MobaTraceKind OriginKind => LineageInput.OriginKind;
        /// <summary>先从链路、再从来源、最后从快照推导出的源角色。</summary>
        public int SourceActorId => LineageInput.SourceActorId != 0 ? LineageInput.SourceActorId : Origin.SourceActorId != 0 ? Origin.SourceActorId : ExecutionSnapshot.SourceActorId;
        /// <summary>先从链路、再从来源、最后从快照推导出的目标角色。</summary>
        public int TargetActorId => LineageInput.TargetActorId != 0 ? LineageInput.TargetActorId : Origin.TargetActorId != 0 ? Origin.TargetActorId : ExecutionSnapshot.TargetActorId;
        /// <summary>当前执行的父上下文；链路缺失时回退到来源或快照节点。</summary>
        public long ParentContextId => LineageInput.ParentContextId != 0 ? LineageInput.ParentContextId : Origin.EffectiveParentContextId != 0 ? Origin.EffectiveParentContextId : ExecutionSnapshot.SourceContextId;
        /// <summary>当前执行链路的有效根上下文。</summary>
        public long RootContextId => LineageInput.EffectiveRootContextId != 0 ? LineageInput.EffectiveRootContextId : Origin.EffectiveRootContextId != 0 ? Origin.EffectiveRootContextId : ExecutionSnapshot.EffectiveRootContextId;
        /// <summary>在链路、来源、快照之间传递的所有权上下文标识。</summary>
        public long OwnerContextId => LineageInput.OwnerContextId != 0 ? LineageInput.OwnerContextId : Origin.OwnerContextId != 0 ? Origin.OwnerContextId : ExecutionSnapshot.OwnerContextId;
        public int TriggerId => ExecutionSnapshot.TriggerId;
        public int ConfigId => ExecutionSnapshot.ConfigId;
        public bool IsValid => LineageInput.SourceActorId != 0 || LineageInput.TargetActorId != 0 || Origin.IsValid || ExecutionSnapshot.IsValid || SkillRuntimeHandle.IsValid || Payload != null;
        /// <summary>是否具备可继续向下游创建执行节点的来源信息。</summary>
        public bool HasExecutionSource => SourceActorId > 0 && ParentContextId != 0;

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            context = this;
            return IsValid;
        }

        /// <summary>尝试以指定强类型读取原始 payload。</summary>
        public bool TryGetOriginalPayload<TPayload>(out TPayload payload)
        {
            if (Payload is TPayload typed)
            {
                payload = typed;
                return true;
            }

            payload = default;
            return false;
        }

        public bool TryGetRuntimeContext(out MobaRuntimeContextReference reference)
        {
            if (Payload is IMobaRuntimeContextPayload provider
                && provider.TryGetRuntimeContext(out reference)
                && reference.IsValid)
            {
                return true;
            }

            reference = default;
            return false;
        }

        public MobaRuntimeContextValueResult<TValue> GetRuntimeContextValue<TValue, TProperty>(
            MobaRuntimeContextService contexts,
            string key,
            ContextValueReadMode mode = ContextValueReadMode.RealtimeThenSnapshot)
            where TProperty : class, IProperty
        {
            return MobaRuntimeContextAccessExtensions.GetRuntimeContextValue<TValue, TProperty>(Payload, contexts, key, mode);
        }

        public bool TryGetRuntimeContextValue<TValue, TProperty>(
            MobaRuntimeContextService contexts,
            string key,
            out TValue value,
            ContextValueReadMode mode = ContextValueReadMode.RealtimeThenSnapshot)
            where TProperty : class, IProperty
        {
            var result = GetRuntimeContextValue<TValue, TProperty>(contexts, key, mode);
            value = result.Value;
            return result.Found;
        }
 
        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (Origin.IsValid)
            {
                origin = Origin;
                return true;
            }

            if (TryGetLineageContext(out var lineageContext))
            {
                var handle = SkillRuntimeHandle;
                origin = MobaGameplayOrigin.FromLineageContext(in lineageContext, in handle);
                return origin.IsValid;
            }

            origin = default;
            return false;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = new MobaTriggerLineageContext(
                ContextKind,
                OriginKind,
                SourceActorId,
                TargetActorId,
                ParentContextId,
                RootContextId,
                OwnerContextId,
                ConfigId);
            return lineageContext.HasExecutionSource;
        }

        public bool TryGetSkillRuntimeHandle(out MobaSkillCastRuntimeHandle handle)
        {
            handle = SkillRuntimeHandle;
            return handle.IsValid;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            snapshot = ExecutionSnapshot;
            return snapshot.IsValid;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            source = new MobaContextSourceView(
                MobaContextSourceResolveKind.CombatExecutionContext,
                MobaContextSourceBoundary.Execution,
                ContextKind,
                OriginKind,
                SourceActorId,
                TargetActorId,
                ParentContextId,
                ParentContextId,
                RootContextId,
                OwnerContextId,
                ConfigId,
                TriggerId,
                Frame,
                null,
                0,
                false,
                SkillRuntimeHandle);
            return source.IsValid;
        }

        public static MobaCombatExecutionContext Create(
            object payload,
            in MobaEffectLineageInput lineageInput,
            in MobaTriggerExecutionSnapshot executionSnapshot,
            int frame)
        {
            return MobaCombatExecutionContextFactory.Create(payload, in lineageInput, in executionSnapshot, frame);
        }

        public MobaCombatExecutionContext WithSnapshot(in MobaTriggerExecutionSnapshot executionSnapshot, int frame)
        {
            return MobaCombatExecutionContextFactory.WithSnapshot(in this, in executionSnapshot, frame);
        }

        public MobaCombatExecutionContext WithExecutionRoot(
            long rootContextId,
            int effectConfigId)
        {
            if (rootContextId == 0L)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(rootContextId),
                    rootContextId,
                    "Execution root context id must be non-zero.");
            }

            var ownerContextId = OwnerContextId != 0L
                ? OwnerContextId
                : rootContextId;
            var configId = effectConfigId != 0
                ? effectConfigId
                : ConfigId;
            var lineageInput = new MobaEffectLineageInput(
                ContextKind,
                MobaTraceKind.EffectExecution,
                SourceActorId,
                TargetActorId,
                rootContextId,
                rootContextId,
                ownerContextId,
                configId);
            var origin = new MobaGameplayOrigin(
                SourceActorId,
                TargetActorId,
                MobaTraceKind.EffectExecution,
                configId,
                rootContextId,
                rootContextId,
                rootContextId,
                ownerContextId,
                SkillRuntimeHandle);
            var snapshot = new MobaTriggerExecutionSnapshot(
                ContextKind,
                SourceActorId,
                TargetActorId,
                rootContextId,
                rootContextId,
                ownerContextId,
                TriggerId,
                configId,
                Frame,
                SkillRuntimeHandle);
            return new MobaCombatExecutionContext(
                Payload,
                lineageInput,
                origin,
                snapshot,
                SkillRuntimeHandle,
                Frame);
        }
    }

    public static class MobaCombatExecutionContextResolveExtensions
    {
        public static bool TryResolveCombatExecutionContext(this object payload, out MobaCombatExecutionContext context)
        {
            context = default;
            if (payload is MobaCombatExecutionContext direct && direct.HasExecutionSource)
            {
                context = direct;
                return true;
            }

            if (payload is IMobaCombatContextSource sourceProvider
                && sourceProvider.TryGetCombatContextSource(out var source)
                && source.HasExecutionSource)
            {
                context = MobaCombatContextBuilder.FromSource(payload, in source);
                return context.HasExecutionSource;
            }

            return payload is IMobaCombatExecutionContextProvider provider
                   && provider.TryGetCombatExecutionContext(out context)
                   && context.HasExecutionSource;
        }
    }
}
