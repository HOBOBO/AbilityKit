using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillPipelineContext : IAbilityPipelineContext<object>, IMobaCombatContextSource, IMobaCombatExecutionContextProvider, IMobaTriggerLineageContextProvider, IMobaTriggerExecutionSnapshotProvider, IMobaTriggerSkillRuntimeContext, IMobaOriginContextProvider, IMobaContextSourceProvider
    {
        public object AbilityInstance { get; private set; }
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        public EAbilityPipelineState PipelineState { get; set; }
        public bool IsAborted { get; set; }
        public bool IsPaused { get; set; }
        public float StartTime { get; set; }
        public float ElapsedTime { get; private set; }

        public MobaSkillCastRuntimeHandle RuntimeHandle { get; set; }
        public long RuntimeId { get; set; }
        public long SourceContextId { get; set; }
        public long PipelineTraceParentContextId { get; set; }
        public string FailReason { get; set; }

        public int SkillId { get; private set; }
        public int CastFlowId { get; private set; }
        public int SkillSlot { get; private set; }
        public int SkillLevel { get; private set; }
        public int CastSequence { get; private set; }
        public int TimelineNextEventIndex { get; private set; }
        public bool InputReleased { get; private set; }
        public int Frame { get; private set; }
        public int CasterActorId { get; private set; }
        public int TargetActorId { get; private set; }
        public Vec3 AimPos { get; private set; }
        public Vec3 AimDir { get; private set; }

        public IWorldResolver WorldServices { get; private set; }
        public AbilityKit.Triggering.Eventing.IEventBus EventBus { get; private set; }
        public IUnitFacade CasterUnit { get; private set; }
        public IUnitFacade TargetUnit { get; private set; }

        public Dictionary<string, object> SharedData { get; } = new();

        /// <summary>
        /// 技能冷却时间（毫秒）
        /// </summary>
        public int SkillCooldownMs { get; set; }

        private readonly List<IDisposable> _disposables = new List<IDisposable>(4);
        private readonly List<Action> _cleanupActions = new List<Action>(4);

        public T GetData<T>(string key, T defaultValue = default)
        {
            if (SharedData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void SetData<T>(string key, T value)
        {
            SharedData[key] = value;
        }

        public bool TryGetData<T>(string key, out T value)
        {
            if (SharedData.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool RemoveData(string key)
        {
            return SharedData.Remove(key);
        }

        public void ClearData()
        {
            SharedData.Clear();
        }

        public void RegisterCleanup(IDisposable disposable)
        {
            if (disposable == null) return;
            _disposables.Add(disposable);
        }

        public void RegisterCleanup(Action action)
        {
            if (action == null) return;
            _cleanupActions.Add(action);
        }

        public void RunAndClearCleanups()
        {
            for (int i = _cleanupActions.Count - 1; i >= 0; i--)
            {
                try { _cleanupActions[i]?.Invoke(); }
                catch (Exception ex) { ReportCleanupException(ex, "action"); }
            }
            _cleanupActions.Clear();

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try { _disposables[i]?.Dispose(); }
                catch (Exception ex) { ReportCleanupException(ex, "disposable"); }
            }
            _disposables.Clear();
        }

        private void ReportCleanupException(Exception ex, string kind)
        {
            try
            {
                var exceptions = WorldServices != null ? WorldServices.Resolve<IMobaBattleExceptionPolicy>() : null;
                if (exceptions != null)
                {
                    exceptions.Handle(
                        ex,
                        new MobaBattleExceptionContext(
                            MobaBattleExceptionDomain.Cleanup,
                            "skill.context.cleanup",
                            actorId: CasterActorId,
                            skillId: SkillId,
                            runtimeId: RuntimeId,
                            detail: "kind=" + kind,
                            rootContextId: RuntimeHandle.RootTraceContextId,
                            sourceContextId: SourceContextId,
                            runtimeHandle: RuntimeHandle),
                        MobaBattleExceptionSeverity.Recoverable);
                    return;
                }
            }
            catch (Exception policyEx)
            {
                AbilityKit.Core.Logging.Log.Exception(
                    policyEx,
                    $"[SkillPipelineContext] Cleanup exception policy failed. kind={kind} actor={CasterActorId} skill={SkillId} runtime={RuntimeId}");
            }

            AbilityKit.Core.Logging.Log.Exception(
                ex,
                $"[SkillPipelineContext] Cleanup failed. kind={kind} actor={CasterActorId} skill={SkillId} runtime={RuntimeId}");
        }

        public void Initialize(object abilityInstance, in SkillCastRequest request, SkillCastContext triggerContext = null)
        {
            AbilityInstance = abilityInstance;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0f;
            ElapsedTime = 0f;

            SharedData.Clear();
            FailReason = null;
            SkillLevel = triggerContext?.SkillLevel ?? 0;
            CastSequence = triggerContext?.Sequence ?? 0;
            TimelineNextEventIndex = 0;
            InputReleased = false;
            Frame = 0;

            _disposables.Clear();
            _cleanupActions.Clear();

            RuntimeHandle = triggerContext != null ? triggerContext.RuntimeHandle : default;
            RuntimeId = RuntimeHandle.IsValid ? RuntimeHandle.RuntimeId : triggerContext?.RuntimeId ?? 0L;
            SourceContextId = triggerContext?.SourceContextId ?? 0L;
            PipelineTraceParentContextId = 0L;

            SkillId = request.SkillId;
            CastFlowId = triggerContext?.CastFlowId ?? 0;
            SkillSlot = request.SkillSlot;
            CasterActorId = request.CasterActorId;
            TargetActorId = request.TargetActorId;
            AimPos = request.AimPos;
            AimDir = request.AimDir;

            WorldServices = request.WorldServices;
            EventBus = request.EventBus;
            CasterUnit = request.CasterUnit;
            TargetUnit = request.TargetUnit;

            // 为仍读取 IAbilityPipelineContext 的通用管线/效果适配器同步核心事实。
            Vec3 aimPos = AimPos;
            Vec3 aimDir = AimDir;
            var runtimeHandle = RuntimeHandle;
            this.SetSkillInfo(SkillId, SkillSlot, SkillLevel);
            this.SetParticipants(CasterActorId, TargetActorId);
            this.SetAim(in aimPos, in aimDir);
            this.SetContextKind((int)EffectContextKind.Skill);
            this.SetSourceContextId(SourceContextId);
            this.SetSkillRuntimeHandle(in runtimeHandle);
        }

        public void SetPipelineTraceLocation(int castFlowId, long parentContextId)
        {
            CastFlowId = castFlowId;
            PipelineTraceParentContextId = parentContextId;
        }

        public void UpdateInput(in Vec3 aimPos, in Vec3 aimDir, int targetActorId)
        {
            if (!aimPos.Equals(Vec3.Zero)) AimPos = aimPos;
            if (!aimDir.Equals(Vec3.Zero)) AimDir = aimDir;
            if (targetActorId > 0) TargetActorId = targetActorId;

            var currentAimPos = AimPos;
            var currentAimDir = AimDir;
            this.SetAim(in currentAimPos, in currentAimDir);
            this.SetParticipants(CasterActorId, TargetActorId);
        }

        public void MarkInputReleased()
        {
            InputReleased = true;
        }

        public bool IsInputReleased()
        {
            return InputReleased;
        }

        public void SetTimelineNextEventIndex(int nextEventIndex)
        {
            TimelineNextEventIndex = nextEventIndex < 0 ? 0 : nextEventIndex;
        }

        public void SetFrame(int frame)
        {
            Frame = frame < 0 ? 0 : frame;
        }

        public void SetCastSequence(int sequence)
        {
            CastSequence = sequence < 0 ? 0 : sequence;
        }

        public int NextCastSequence()
        {
            CastSequence++;
            return CastSequence;
        }

        public void AdvanceTime(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            ElapsedTime += deltaTime;
        }

        public bool TryGetCombatContextSource(out MobaCombatContextSource source)
        {
            TryGetSkillRuntimeHandle(out var handle);
            var sourceContextId = SourceContextId != 0L ? SourceContextId : this.GetSourceContextId();
            source = MobaCombatContextBuilder.SkillCast(
                SkillId,
                CasterActorId,
                TargetActorId,
                sourceContextId,
                Frame,
                in handle);
            return source.IsValid;
        }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            return MobaCombatContextBuilder.TryFromSource(this, out context);
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = default;
            if (!TryGetCombatContextSource(out var source)) return false;

            lineageContext = source.ToLineageContext();
            return true;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetCombatContextSource(out var source)) return false;

            snapshot = source.ToExecutionSnapshot();
            return snapshot.IsValid;
        }

        public bool TryGetSkillRuntimeHandle(out MobaSkillCastRuntimeHandle handle)
        {
            handle = RuntimeHandle.IsValid ? RuntimeHandle : this.GetSkillRuntimeHandle();
            return handle.IsValid;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = default;
            if (!TryGetCombatContextSource(out var source)) return false;

            origin = source.ToOrigin();
            return origin.IsValid;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            source = default;
            if (!TryGetCombatContextSource(out var combatSource)) return false;

            source = combatSource.ToContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime);
            return source.IsValid;
        }

        public void Reset()
        {
            AbilityInstance = null;
            CurrentPhaseId = default;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0f;
            ElapsedTime = 0f;

            SharedData.Clear();

            _disposables.Clear();
            _cleanupActions.Clear();

            RuntimeHandle = default;
            RuntimeId = 0L;
            this.SetSourceContextId(0L);
            FailReason = null;

            SkillId = 0;
            SkillSlot = 0;
            SkillLevel = 0;
            CastSequence = 0;
            TimelineNextEventIndex = 0;
            InputReleased = false;
            Frame = 0;
            CasterActorId = 0;
            TargetActorId = 0;
            AimPos = Vec3.Zero;
            AimDir = Vec3.Forward;

            WorldServices = null;
            EventBus = null;
            CasterUnit = null;
            TargetUnit = null;
        }
    }

    public readonly struct SkillCastRequest
    {
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int CasterActorId;
        public readonly int TargetActorId;
        public readonly Vec3 AimPos;
        public readonly Vec3 AimDir;

        public readonly IWorldResolver WorldServices;
        public readonly AbilityKit.Triggering.Eventing.IEventBus EventBus;
        public readonly IUnitFacade CasterUnit;
        public readonly IUnitFacade TargetUnit;

        public SkillCastRequest(
            int skillId,
            int skillSlot,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPos,
            in Vec3 aimDir,
            IWorldResolver worldServices,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            IUnitFacade casterUnit,
            IUnitFacade targetUnit)
        {
            SkillId = skillId;
            SkillSlot = skillSlot;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            WorldServices = worldServices;
            EventBus = eventBus;
            CasterUnit = casterUnit;
            TargetUnit = targetUnit;
        }
    }
}
