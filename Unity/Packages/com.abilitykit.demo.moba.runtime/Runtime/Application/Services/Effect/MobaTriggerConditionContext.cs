using System;
using AbilityKit.Context;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaTriggerConditionContext : IMobaTriggerStageSnapshotProvider, IMobaRuntimeContextPayload
    {
        private readonly object _payload;
        private readonly MobaSkillCastRuntimeService _skillRuntimes;

        private MobaTriggerConditionContext(
            object payload,
            MobaEffectLineageInput lineageInput,
            MobaGameplayOrigin origin,
            MobaTriggerExecutionSnapshot executionSnapshot,
            MobaSkillCastRuntimeHandle skillRuntimeHandle,
            MobaTriggerStageSnapshot stageSnapshot,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame)
        {
            _payload = payload;
            _skillRuntimes = skillRuntimes;
            LineageInput = lineageInput;
            Origin = origin;
            ExecutionSnapshot = executionSnapshot;
            StageSnapshot = stageSnapshot;
            SkillRuntimeHandle = skillRuntimeHandle;
            Frame = frame != 0 ? frame : executionSnapshot.Frame;
        }

        public object Payload => _payload;
        public MobaEffectLineageInput LineageInput { get; }
        public MobaEffectTraceInput TraceInput => LineageInput.ToTraceInput();
        public MobaGameplayOrigin Origin { get; }
        public MobaTriggerExecutionSnapshot ExecutionSnapshot { get; }
        public MobaTriggerStageSnapshot StageSnapshot { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        public int Frame { get; }
        public EffectContextKind ContextKind => LineageInput.ContextKind != EffectContextKind.Unknown ? LineageInput.ContextKind : ExecutionSnapshot.Kind;
        public MobaTraceKind OriginKind => LineageInput.OriginKind;
        public int SourceActorId => LineageInput.SourceActorId != 0 ? LineageInput.SourceActorId : Origin.SourceActorId != 0 ? Origin.SourceActorId : ExecutionSnapshot.SourceActorId;
        public int TargetActorId => LineageInput.TargetActorId != 0 ? LineageInput.TargetActorId : Origin.TargetActorId != 0 ? Origin.TargetActorId : ExecutionSnapshot.TargetActorId;
        public long ParentContextId => LineageInput.ParentContextId != 0 ? LineageInput.ParentContextId : Origin.EffectiveParentContextId != 0 ? Origin.EffectiveParentContextId : ExecutionSnapshot.SourceContextId;
        public long RootContextId => LineageInput.EffectiveRootContextId != 0 ? LineageInput.EffectiveRootContextId : Origin.EffectiveRootContextId != 0 ? Origin.EffectiveRootContextId : ExecutionSnapshot.EffectiveRootContextId;
        public long OwnerContextId => LineageInput.OwnerContextId != 0 ? LineageInput.OwnerContextId : Origin.OwnerContextId != 0 ? Origin.OwnerContextId : ExecutionSnapshot.OwnerContextId;
        public int TriggerId => ExecutionSnapshot.TriggerId;
        public int ConfigId => ExecutionSnapshot.ConfigId;
        public int StackCount => StageSnapshot.StackCount;
        public float ElapsedSeconds => StageSnapshot.ElapsedSeconds;
        public float RemainingSeconds => StageSnapshot.RemainingSeconds;
        public float DurationSeconds => StageSnapshot.DurationSeconds;
        public bool HasSkillRuntime => SkillRuntimeHandle.IsValid;
        public bool HasExecutionSource => SourceActorId > 0 && ParentContextId != 0;
        public bool CanCreateRootExecution => SourceActorId > 0 && ParentContextId == 0;

        public bool TryGetPayload<TPayload>(out TPayload payload)
        {
            if (_payload is TPayload typed)
            {
                payload = typed;
                return true;
            }

            payload = default;
            return false;
        }

        public bool TryGetRuntimeContext(out MobaRuntimeContextReference reference)
        {
            return _payload.TryResolveRuntimeContext(out reference);
        }

        public MobaRuntimeContextValueResult<TValue> GetRuntimeContextValue<TValue, TProperty>(
            MobaRuntimeContextService contexts,
            string key,
            ContextValueReadMode mode = ContextValueReadMode.RealtimeThenSnapshot)
            where TProperty : class, IProperty
        {
            return _payload.GetRuntimeContextValue<TValue, TProperty>(contexts, key, mode);
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
 
        public bool TryGetBlackboard(out MobaSkillRuntimeBlackboard blackboard)
        {
            blackboard = null;
            if (!SkillRuntimeHandle.IsValid || _skillRuntimes == null) return false;
            var handle = SkillRuntimeHandle;
            return _skillRuntimes.TryGetBlackboard(in handle, out blackboard);
        }

        public bool HasDamagedTarget(int actorId)
        {
            return TryGetBlackboard(out var blackboard) && blackboard.ContainsActorId(in MobaSkillRuntimeBlackboardKeys.DamagedTargets, actorId);
        }

        public int GetHitCount()
        {
            if (!TryGetBlackboard(out var blackboard)) return 0;
            return blackboard.TryGet(in MobaSkillRuntimeBlackboardKeys.HitCount, out var value) ? value.IntValue : 0;
        }

        public bool HasLoopGuard(long contextId)
        {
            return TryGetBlackboard(out var blackboard) && blackboard.ContainsContextId(in MobaSkillRuntimeBlackboardKeys.LoopGuards, contextId);
        }

        public MobaTriggerExecutionRequest ToExecutionRequest(int triggerId)
        {
            if (!HasExecutionSource && !CanCreateRootExecution)
            {
                throw new InvalidOperationException($"MobaTriggerConditionContext requires an execution source or root capability before creating execution request. triggerId={triggerId}, frame={Frame}, sourceActorId={SourceActorId}, parentContextId={ParentContextId}, rootContextId={RootContextId}, contextKind={ContextKind}, originKind={OriginKind}");
            }

            return new MobaTriggerExecutionRequest(
                triggerId,
                Frame,
                RootContextId,
                ParentContextId,
                SourceActorId,
                TargetActorId,
                ContextKind,
                OriginKind);
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            snapshot = ExecutionSnapshot;
            return snapshot.IsValid;
        }

        public bool TryGetStageSnapshot(out MobaTriggerStageSnapshot snapshot)
        {
            snapshot = StageSnapshot;
            return snapshot.IsValid;
        }

        public static MobaTriggerConditionContext Create(
            object payload,
            in MobaEffectLineageInput lineageInput,
            in MobaTriggerExecutionSnapshot executionSnapshot,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame)
        {
            var executionContext = MobaCombatExecutionContextFactory.Create(payload, in lineageInput, in executionSnapshot, frame);
            return Create(in executionContext, skillRuntimes, frame);
        }

        public static MobaTriggerConditionContext Create(
            in MobaCombatExecutionContext executionContext,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame)
        {
            var payload = executionContext.HasOriginalPayload ? executionContext.Payload : null;
            payload.TryResolveStageSnapshot(out var stageSnapshot);
            return new MobaTriggerConditionContext(
                payload,
                executionContext.LineageInput,
                executionContext.Origin,
                executionContext.ExecutionSnapshot,
                executionContext.SkillRuntimeHandle,
                stageSnapshot,
                skillRuntimes,
                frame != 0 ? frame : executionContext.Frame);
        }

        public static MobaTriggerConditionContext Create(
            object payload,
            in MobaEffectTraceInput traceInput,
            in MobaTriggerExecutionSnapshot executionSnapshot,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame)
        {
            var lineageInput = traceInput.ToLineageInput();
            return Create(payload, in lineageInput, in executionSnapshot, skillRuntimes, frame);
        }

        public static MobaTriggerConditionContext Create(
            object payload,
            in MobaEffectLineageInput lineageInput,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame)
        {
            var snapshot = default(MobaTriggerExecutionSnapshot);
            return Create(payload, in lineageInput, in snapshot, skillRuntimes, frame);
        }

        public static MobaTriggerConditionContext Create(
            object payload,
            in MobaEffectTraceInput traceInput,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame)
        {
            var lineageInput = traceInput.ToLineageInput();
            return Create(payload, in lineageInput, skillRuntimes, frame);
        }
    }
}
