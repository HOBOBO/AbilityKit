using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Core.Mathematics;
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

        // Q32.32 raw 时间累计（整数加法无漂移）；float 属性是触发/表现边界的单次换算视图。
        private long _elapsedRaw;

        public float ElapsedTime => Deterministic.Fixed64.FromRaw(_elapsedRaw).ToSingle();

        public MobaSkillCastRuntimeHandle RuntimeHandle { get; set; }
        public long RuntimeId { get; set; }
        public long SourceContextId { get; set; }
        public long DiagnosticCommandId { get; private set; }
        public string FailReason { get; set; }

        public int SkillId { get; private set; }
        public int CastFlowId { get; private set; }
        public int SkillSlot { get; private set; }
        public int SkillLevel { get; private set; }
        public ResolvedSkillCastConfiguration ResolvedConfiguration { get; private set; }
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

        internal void DiscardCleanupsForRollback()
        {
            _cleanupActions.Clear();
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
                            rootContextId: RuntimeHandle.RootContextId,
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
            _elapsedRaw = 0L;

            SharedData.Clear();
            FailReason = null;
            SkillLevel = triggerContext?.SkillLevel ?? 0;
            ResolvedConfiguration = triggerContext?.ResolvedConfiguration ?? default;
            SkillCooldownMs = ResolvedConfiguration.CooldownMs;
            CastSequence = triggerContext?.Sequence ?? 0;
            TimelineNextEventIndex = 0;
            InputReleased = false;
            Frame = 0;

            _disposables.Clear();
            _cleanupActions.Clear();

            RuntimeHandle = triggerContext != null ? triggerContext.RuntimeHandle : default;
            RuntimeId = RuntimeHandle.IsValid ? RuntimeHandle.RuntimeId : triggerContext?.RuntimeId ?? 0L;
            SourceContextId = triggerContext?.SourceContextId ?? 0L;
            DiagnosticCommandId = triggerContext?.DiagnosticCommandId ?? 0L;

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

        public void SetCastFlowId(int castFlowId)
        {
            CastFlowId = castFlowId;
        }

        public void UpdateInput(in Vec3 aimPos, in Vec3 aimDir, int targetActorId)
        {
            UpdateInput(in aimPos, in aimDir, targetActorId,
                !aimPos.Equals(Vec3.Zero), !aimDir.Equals(Vec3.Zero), targetActorId > 0);
        }

        public void UpdateInput(in Vec3 aimPos, in Vec3 aimDir, int targetActorId,
            bool hasAimPos, bool hasAimDir, bool hasTarget)
        {
            if (hasAimPos) AimPos = aimPos;
            if (hasAimDir && !aimDir.Equals(Vec3.Zero)) AimDir = aimDir;
            if (hasTarget)
            {
                TargetActorId = targetActorId > 0 ? targetActorId : 0;
                TargetUnit = null;
                if (TargetActorId > 0 && WorldServices != null &&
                    WorldServices.TryResolve<IUnitResolver>(out var units) && units != null)
                {
                    if (units.TryResolve(new EcsEntityId(TargetActorId), out var targetUnit))
                        TargetUnit = targetUnit;
                }
            }

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
            _elapsedRaw += DeterministicMathBridge.ToFixed(deltaTime).RawValue;
        }

        internal SkillPipelineContextRollbackState CaptureRollbackState()
        {
            var keys = new List<string>(SharedData.Keys);
            keys.Sort(StringComparer.Ordinal);
            var shared = new SkillSharedDataRollbackEntry[keys.Count];
            for (var i = 0; i < keys.Count; i++)
                shared[i] = SkillSharedDataRollbackEntry.Capture(keys[i], SharedData[keys[i]]);
            return new SkillPipelineContextRollbackState(
                _elapsedRaw, Frame, TargetActorId, AimPos, AimDir, CastSequence,
                TimelineNextEventIndex, InputReleased, SkillCooldownMs, FailReason,
                SourceContextId, DiagnosticCommandId, CastFlowId, ResolvedConfiguration,
                IsAborted, StartTime, shared);
        }

        internal void RestoreRollbackState(in SkillPipelineContextRollbackState state)
        {
            if (state.ElapsedRaw < 0 || state.Frame < 0 || state.TargetActorId < 0 ||
                state.CastSequence < 0 || state.TimelineNextEventIndex < 0 ||
                state.SkillCooldownMs < 0 || state.CastFlowId < 0)
                throw new InvalidOperationException("Invalid skill pipeline context rollback state.");
            _elapsedRaw = state.ElapsedRaw;
            SetFrame(state.Frame);
            SetCastSequence(state.CastSequence);
            SetTimelineNextEventIndex(state.TimelineNextEventIndex);
            SkillCooldownMs = state.SkillCooldownMs;
            FailReason = state.FailReason;
            SourceContextId = state.SourceContextId;
            DiagnosticCommandId = state.DiagnosticCommandId;
            CastFlowId = state.CastFlowId;
            ResolvedConfiguration = state.ResolvedConfiguration;
            var aimPos = state.AimPos;
            var aimDir = state.AimDir;
            UpdateInput(in aimPos, in aimDir, state.TargetActorId,
                hasAimPos: true, hasAimDir: true, hasTarget: true);
            InputReleased = state.InputReleased;
            IsAborted = state.IsAborted;
            StartTime = state.StartTime;
            this.SetSourceContextId(SourceContextId);
            SharedData.Clear();
            foreach (var item in state.SharedData) SharedData.Add(item.Key, item.ToValue());
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
            _elapsedRaw = 0L;

            SharedData.Clear();

            _disposables.Clear();
            _cleanupActions.Clear();

            RuntimeHandle = default;
            RuntimeId = 0L;
            DiagnosticCommandId = 0L;
            this.SetSourceContextId(0L);
            FailReason = null;

            SkillId = 0;
            SkillSlot = 0;
            SkillLevel = 0;
            ResolvedConfiguration = default;
            SkillCooldownMs = 0;
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

    internal readonly struct SkillPipelineContextRollbackState
    {
        public SkillPipelineContextRollbackState(long elapsedRaw, int frame, int targetActorId,
            Vec3 aimPos, Vec3 aimDir, int castSequence, int timelineNextEventIndex,
            bool inputReleased, int skillCooldownMs, string failReason, long sourceContextId,
            long diagnosticCommandId, int castFlowId,
            ResolvedSkillCastConfiguration resolvedConfiguration, bool isAborted, float startTime,
            SkillSharedDataRollbackEntry[] sharedData)
        {
            ElapsedRaw = elapsedRaw;
            Frame = frame;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            CastSequence = castSequence;
            TimelineNextEventIndex = timelineNextEventIndex;
            InputReleased = inputReleased;
            SkillCooldownMs = skillCooldownMs;
            FailReason = failReason;
            SourceContextId = sourceContextId;
            DiagnosticCommandId = diagnosticCommandId;
            CastFlowId = castFlowId;
            ResolvedConfiguration = resolvedConfiguration;
            IsAborted = isAborted;
            StartTime = startTime;
            SharedData = sharedData ?? Array.Empty<SkillSharedDataRollbackEntry>();
        }

        public long ElapsedRaw { get; }
        public int Frame { get; }
        public int TargetActorId { get; }
        public Vec3 AimPos { get; }
        public Vec3 AimDir { get; }
        public int CastSequence { get; }
        public int TimelineNextEventIndex { get; }
        public bool InputReleased { get; }
        public int SkillCooldownMs { get; }
        public string FailReason { get; }
        public long SourceContextId { get; }
        public long DiagnosticCommandId { get; }
        public int CastFlowId { get; }
        public ResolvedSkillCastConfiguration ResolvedConfiguration { get; }
        public bool IsAborted { get; }
        public float StartTime { get; }
        public SkillSharedDataRollbackEntry[] SharedData { get; }
    }

    internal readonly struct SkillSharedDataRollbackEntry
    {
        public const byte Null = 0, Integer = 1, Long = 2, Boolean = 3,
            Float = 4, Double = 5, String = 6, Vector = 7, RuntimeHandle = 8;

        public SkillSharedDataRollbackEntry(string key, byte kind, int integer, long longValue,
            double number, string text, Vec3 vector, MobaSkillCastRuntimeHandle handle)
        {
            Key = key;
            Kind = kind;
            IntegerValue = integer;
            LongValue = longValue;
            Number = number;
            Text = text;
            VectorValue = vector;
            Handle = handle;
        }

        public string Key { get; }
        public byte Kind { get; }
        public int IntegerValue { get; }
        public long LongValue { get; }
        public double Number { get; }
        public string Text { get; }
        public Vec3 VectorValue { get; }
        public MobaSkillCastRuntimeHandle Handle { get; }

        public static SkillSharedDataRollbackEntry Capture(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Skill shared-data key is empty.");
            switch (value)
            {
                case null: return new SkillSharedDataRollbackEntry(key, Null, 0, 0, 0, null, default, default);
                case int v: return new SkillSharedDataRollbackEntry(key, Integer, v, 0, 0, null, default, default);
                case long v: return new SkillSharedDataRollbackEntry(key, Long, 0, v, 0, null, default, default);
                case bool v: return new SkillSharedDataRollbackEntry(key, Boolean, v ? 1 : 0, 0, 0, null, default, default);
                case float v: return new SkillSharedDataRollbackEntry(key, Float, 0, 0, v, null, default, default);
                case double v: return new SkillSharedDataRollbackEntry(key, Double, 0, 0, v, null, default, default);
                case string v: return new SkillSharedDataRollbackEntry(key, String, 0, 0, 0, v, default, default);
                case Vec3 v: return new SkillSharedDataRollbackEntry(key, Vector, 0, 0, 0, null, v, default);
                case MobaSkillCastRuntimeHandle v:
                    return new SkillSharedDataRollbackEntry(key, RuntimeHandle, 0, 0, 0, null, default, v);
                default: throw new InvalidOperationException($"Skill shared-data key '{key}' has unsupported rollback type '{value.GetType().FullName}'.");
            }
        }

        public object ToValue()
        {
            switch (Kind)
            {
                case Null: return null;
                case Integer: return IntegerValue;
                case Long: return LongValue;
                case Boolean: return IntegerValue != 0;
                case Float: return (float)Number;
                case Double: return Number;
                case String: return Text;
                case Vector: return VectorValue;
                case RuntimeHandle: return Handle;
                default: throw new InvalidOperationException("Invalid skill shared-data rollback kind.");
            }
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
