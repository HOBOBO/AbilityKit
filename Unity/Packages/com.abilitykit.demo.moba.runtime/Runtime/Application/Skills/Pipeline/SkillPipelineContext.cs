using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Pipeline;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Modifiers;

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
        public MobaSummonInputIdentity SummonInputIdentity { get; private set; }
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

        private string _characterModeId;
        private int _characterModePriority;
        private long _characterModeOwnerId;
        private long _characterModeInstanceId;
        private bool _characterModeCleanupRegistered;
        private bool _hfsmParameterCleanupRegistered;
        private long _hfsmParameterOwnerId;

        public string CharacterModeId => _characterModeId ?? string.Empty;
        public long CharacterModeOwnerId => _characterModeOwnerId;
        public long CharacterModeInstanceId
        {
            get
            {
                RefreshCharacterModeInstance();
                return _characterModeInstanceId;
            }
        }

        /// <summary>Requests a character mode owned by this cast. The binding is rollback state,
        /// and its exit is reattached after rollback instead of relying on a transient closure.</summary>
        public bool TrySetCharacterMode(string modeId, int priority)
        {
            if (string.IsNullOrWhiteSpace(modeId) || modeId.Length > 64 || priority < 0 ||
                !TryGetCharacterHfsm(out var hfsm)) return false;
            var ownerId = ResolveCharacterModeOwnerId();
            if (ownerId <= 0) return false;
            if (!hfsm.RequestModeEnter(modeId, priority, ownerId)) return false;
            _characterModeId = modeId;
            _characterModePriority = priority;
            _characterModeOwnerId = ownerId;
            _characterModeInstanceId = 0L;
            RegisterCharacterModeCleanup();
            return true;
        }

        public bool TryClearCharacterMode(
            MobaCharacterModeTransitionReason reason = MobaCharacterModeTransitionReason.ExitRequested)
        {
            if (string.IsNullOrEmpty(_characterModeId) || _characterModeOwnerId <= 0) return false;
            var requested = RequestCharacterModeExit(reason);
            if (!requested) return false;
            _characterModeId = null;
            _characterModePriority = 0;
            _characterModeOwnerId = 0L;
            _characterModeInstanceId = 0L;
            return requested;
        }

        /// <summary>Writes a source-owned HFSM parameter for this cast. The cast
        /// owns the contribution until completion, cancellation, death or rollback.</summary>
        public bool TrySetHfsmParameter(int parameterId, float value, int priority = 0)
        {
            if (WorldServices == null || CasterActorId <= 0 ||
                !MobaCharacterHfsmParameterService.IsValidParameterId(parameterId) ||
                !WorldServices.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) || parameters == null)
                return false;
            if (!parameters.SupportsOperation(CasterActorId, parameterId, (int)ModifierOp.Override)) return false;
            var ownerId = SourceContextId > 0 ? SourceContextId : RuntimeId;
            if (ownerId <= 0) return false;
            var modifier = ModifierData.Override(ModifierKey.FromPacked((uint)parameterId), value,
                sourceId: SkillId);
            modifier.Priority = priority;
            parameters.Set(CasterActorId, ownerId, parameterId, in modifier);
            _hfsmParameterOwnerId = ownerId;
            RegisterHfsmParameterCleanup();
            return true;
        }

        private const string PreparedParameterPrefix = "cast.prepared.parameter.";
        private const string ParametersCommittedKey = "cast.prepared.committed";
        private MobaPreparedParameterSnapshot[] _preparedParameters = Array.Empty<MobaPreparedParameterSnapshot>();
        /*
         * v4 快照可以省略提交标记，也可以显式保存 false。
         * 两者都允许后续提交，备份往返时保留原来的条目形态。
         */
        private bool? _parametersCommitted;

        /// <summary>
        /// 返回本次施法的预采样值副本，按目标参数 ID 升序排列。
        /// 调用方可修改结果，不影响当前施法或其它备份。
        /// </summary>
        public MobaPreparedParameterSnapshot[] GetPreparedParameterSnapshots()
        {
            return _preparedParameters.Length == 0 ? Array.Empty<MobaPreparedParameterSnapshot>() :
                (MobaPreparedParameterSnapshot[])_preparedParameters.Clone();
        }

        private static MobaPreparedParameterSnapshot[] CopyPreparedParameters(MobaPreparedParameterSnapshot[] samples)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<MobaPreparedParameterSnapshot>();
            var copy = (MobaPreparedParameterSnapshot[])samples.Clone();
            Array.Sort(copy, ComparePreparedParameterIds);
            for (var i = 1; i < copy.Length; i++)
                if (copy[i - 1].TargetParameterId == copy[i].TargetParameterId)
                    throw new ArgumentException($"Duplicate prepared cast parameter {copy[i].TargetParameterId}.", nameof(samples));
            return copy;
        }

        private static bool IsPreparedParameterKey(string key) =>
            key != null && key.StartsWith(PreparedParameterPrefix, StringComparison.Ordinal);

        private static string GetPreparedParameterKey(int id) =>
            PreparedParameterPrefix + id.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// 校验 v4 载荷中为施法状态保留的键，同时解码预采样值。
        /// 其它共享键仍由通用共享条目处理。
        /// </summary>
        internal static bool TryReadPreparedParameterRollbackEntry(in SkillSharedDataRollbackEntry entry,
            out MobaPreparedParameterSnapshot sample, out bool? committed)
        {
            sample = default;
            committed = null;
            if (entry.Key == ParametersCommittedKey)
            {
                if (entry.Kind != SkillSharedDataRollbackEntry.Boolean || entry.IntegerValue is < 0 or > 1)
                    throw new InvalidDataException($"Invalid prepared cast parameter state '{entry.Key}'.");
                committed = entry.IntegerValue != 0;
                return true;
            }
            if (!IsPreparedParameterKey(entry.Key)) return false;
            if (entry.Kind != SkillSharedDataRollbackEntry.Float ||
                !int.TryParse(entry.Key.Substring(PreparedParameterPrefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var parameterId) || parameterId <= 0 ||
                entry.Key != GetPreparedParameterKey(parameterId) ||
                double.IsNaN(entry.Number) || entry.Number < -float.MaxValue || entry.Number > float.MaxValue)
                throw new InvalidDataException($"Invalid prepared cast parameter state '{entry.Key}'.");
            sample = new MobaPreparedParameterSnapshot(parameterId, (float)entry.Number);
            return true;
        }

        private SkillSharedDataRollbackEntry[] CaptureSharedDataRollbackEntries()
        {
            var entries = new SkillSharedDataRollbackEntry[SharedData.Count + _preparedParameters.Length +
                (_parametersCommitted.HasValue ? 1 : 0)];
            var index = 0;
            foreach (var pair in SharedData)
            {
                if (pair.Key == ParametersCommittedKey || IsPreparedParameterKey(pair.Key))
                    throw new InvalidOperationException(
                        $"Reserved SkillPipelineContext rollback key '{pair.Key}' is present in SharedData.");
                entries[index++] = SkillSharedDataRollbackEntry.Capture(pair.Key, pair.Value);
            }

            foreach (var sample in _preparedParameters)
            {
                var key = GetPreparedParameterKey(sample.TargetParameterId);
                if (sample.TargetParameterId <= 0 || float.IsNaN(sample.Value) || float.IsInfinity(sample.Value))
                    throw new InvalidOperationException($"Invalid SkillPipelineContext rollback value '{key}'.");
                entries[index++] = new SkillSharedDataRollbackEntry(key, SkillSharedDataRollbackEntry.Float,
                    0, 0L, sample.Value, null, default, default);
            }
            if (_parametersCommitted.HasValue)
                entries[index] = SkillSharedDataRollbackEntry.Capture(ParametersCommittedKey, _parametersCommitted.Value);
            Array.Sort(entries, (left, right) => string.CompareOrdinal(left.Key, right.Key));
            return entries;
        }

        private static void ReadPreparedParameterRollbackState(
            SkillSharedDataRollbackEntry[] savedEntries,
            out MobaPreparedParameterSnapshot[] preparedParameters,
            out bool? parametersCommitted,
            out Dictionary<string, object> sharedData)
        {
            List<MobaPreparedParameterSnapshot> prepared = null;
            sharedData = new Dictionary<string, object>(savedEntries.Length);
            parametersCommitted = null;
            string previousKey = null;
            foreach (var entry in savedEntries)
            {
                if (string.IsNullOrEmpty(entry.Key) || previousKey != null && string.CompareOrdinal(previousKey, entry.Key) >= 0)
                    throw new InvalidDataException($"Invalid or repeated skill shared-data rollback key '{entry.Key}'.");
                previousKey = entry.Key;
                if (!TryReadPreparedParameterRollbackEntry(in entry, out var sample, out var committed))
                    sharedData.Add(entry.Key, entry.ToValue());
                else if (committed.HasValue)
                    parametersCommitted = committed;
                else
                    (prepared ??= new List<MobaPreparedParameterSnapshot>()).Add(sample);
            }

            preparedParameters = prepared == null ? Array.Empty<MobaPreparedParameterSnapshot>() : prepared.ToArray();
            Array.Sort(preparedParameters, ComparePreparedParameterIds);
        }

        private static int ComparePreparedParameterIds(MobaPreparedParameterSnapshot left, MobaPreparedParameterSnapshot right) =>
            left.TargetParameterId.CompareTo(right.TargetParameterId);

        internal bool CanCommitPreparedParameters()
        {
            if (_preparedParameters.Length == 0) return true;
            if (WorldServices == null || (SourceContextId <= 0 && RuntimeId <= 0) ||
                !WorldServices.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) || parameters == null) return false;
            foreach (var sample in _preparedParameters)
                if (float.IsNaN(sample.Value) || float.IsInfinity(sample.Value) ||
                    !parameters.SupportsOperation(CasterActorId, sample.TargetParameterId, (int)ModifierOp.Override)) return false;
            return true;
        }

        /// <summary>
        /// 在原提交点应用已冻结的参数；本次施法已提交时直接返回成功。
        /// 提交失败仍沿原来源清理路径撤销已写入的参数。
        /// </summary>
        public bool CommitPreparedParameters()
        {
            if (_parametersCommitted == true) return true;
            if (!CanCommitPreparedParameters()) return false;
            var samples = _preparedParameters;
            foreach (var sample in samples)
                if (!TrySetHfsmParameter(sample.TargetParameterId, sample.Value))
                {
                    TryClearHfsmParameters();
                    return false;
                }
            _parametersCommitted = true;
            if (samples.Length > 0) SynchronizeCharacterHfsm();
            return true;
        }

        internal bool PreservesCharacterCastContext => WorldServices != null &&
            WorldServices.TryResolve<AbilityKit.Demo.Moba.Config.Core.MobaConfigDatabase>(out var configs) &&
            configs.TryGetSkillFlow(CastFlowId, out var flow) && flow.PreserveCharacterCastContext;

        internal void SynchronizeCharacterHfsm()
        {
            if (PreservesCharacterCastContext) return;
            if (TryGetCharacterHfsm(out var hfsm))
            {
                var basicSlot = 0;
                if (WorldServices.TryResolve<MobaActorLookupService>(out var actors) &&
                    actors.TryGetActorEntity(CasterActorId, out var actor) && actor.hasSkillLoadout)
                    basicSlot = actor.skillLoadout.ActiveSkills?.Length ?? 0;
                hfsm.SyncCastContext(SourceContextId, SkillId, SkillSlot, basicSlot);
                hfsm.SyncAim(AimPos, AimDir);
                hfsm.ReevaluateCurrentFrame();
            }
        }

        public bool TryClearHfsmParameters()
        {
            if (_hfsmParameterOwnerId <= 0 || WorldServices == null || CasterActorId <= 0 ||
                !WorldServices.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) || parameters == null)
                return false;
            parameters.ClearSource(CasterActorId, _hfsmParameterOwnerId);
            _hfsmParameterOwnerId = 0L;
            return true;
        }

        private void RegisterHfsmParameterCleanup()
        {
            if (_hfsmParameterCleanupRegistered) return;
            _hfsmParameterCleanupRegistered = true;
            RegisterCleanup(() =>
            {
                if (_hfsmParameterOwnerId > 0 && WorldServices != null && CasterActorId > 0 &&
                    WorldServices.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) && parameters != null)
                    parameters.ClearSource(CasterActorId, _hfsmParameterOwnerId);
                _hfsmParameterOwnerId = 0L;
                _hfsmParameterCleanupRegistered = false;
            });
        }

        private long ResolveCharacterModeOwnerId() =>
            SourceContextId > 0 ? SourceContextId : RuntimeHandle.IsValid ? RuntimeHandle.RuntimeId : 0L;

        private bool TryGetCharacterHfsm(out MobaCharacterHfsmRuntime hfsm)
        {
            hfsm = null;
            if (WorldServices == null || CasterActorId <= 0 ||
                !WorldServices.TryResolve<MobaActorLookupService>(out var actors) || actors == null ||
                !actors.TryGetActorEntity(CasterActorId, out var actor) || actor == null ||
                !actor.hasCharacterHfsm) return false;
            hfsm = actor.characterHfsm.Runtime;
            return hfsm != null;
        }

        private void RefreshCharacterModeInstance()
        {
            if (_characterModeOwnerId <= 0 || !TryGetCharacterHfsm(out var hfsm)) return;
            var mode = hfsm.Mode;
            if (mode.OwnerInstanceId == _characterModeOwnerId &&
                string.Equals(mode.ModeId, _characterModeId, StringComparison.Ordinal))
                _characterModeInstanceId = mode.InstanceId;
            else
                _characterModeInstanceId = 0L;
        }

        private bool RequestCharacterModeExit(MobaCharacterModeTransitionReason reason)
        {
            if (!TryGetCharacterHfsm(out var hfsm)) return false;
            return hfsm.RequestModeExit(_characterModeId, _characterModeOwnerId,
                -1, reason);
        }

        private void RegisterCharacterModeCleanup()
        {
            if (_characterModeCleanupRegistered) return;
            _characterModeCleanupRegistered = true;
            RegisterCleanup(() =>
            {
                if (_characterModeOwnerId > 0 && !string.IsNullOrEmpty(_characterModeId))
                    RequestCharacterModeExit(MobaCharacterModeTransitionReason.Interrupted);
                _characterModeId = null;
                _characterModePriority = 0;
                _characterModeOwnerId = 0L;
                _characterModeInstanceId = 0L;
                _characterModeCleanupRegistered = false;
            });
        }

        public IWorldResolver WorldServices { get; private set; }
        internal SkillPipelineContinuousRuntime PipelineContinuous { get; set; }
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

        /// <summary>
        /// 清除阶段间共享值，以及原先由共享键持有的预采样值和提交标记。
        /// 此操作不提交参数，也不执行清理回调。
        /// </summary>
        public void ClearData()
        {
            SharedData.Clear();
            _preparedParameters = Array.Empty<MobaPreparedParameterSnapshot>();
            _parametersCommitted = null;
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
            _preparedParameters = CopyPreparedParameters(request.ParameterSnapshots);
            _parametersCommitted = null;
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
            _characterModeId = null;
            _characterModePriority = 0;
            _characterModeOwnerId = 0L;
            _characterModeInstanceId = 0L;
            _characterModeCleanupRegistered = false;
            _hfsmParameterOwnerId = 0L;
            _hfsmParameterCleanupRegistered = false;

            RuntimeHandle = triggerContext != null ? triggerContext.RuntimeHandle : default;
            RuntimeId = RuntimeHandle.IsValid ? RuntimeHandle.RuntimeId : triggerContext?.RuntimeId ?? 0L;
            SourceContextId = triggerContext?.SourceContextId ?? 0L;
            DiagnosticCommandId = triggerContext?.DiagnosticCommandId ?? 0L;
            SummonInputIdentity = request.SummonInputIdentity;

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
            var shared = CaptureSharedDataRollbackEntries();
            RefreshCharacterModeInstance();
            return new SkillPipelineContextRollbackState(
                _elapsedRaw, Frame, TargetActorId, AimPos, AimDir, CastSequence,
                TimelineNextEventIndex, InputReleased, SkillCooldownMs, FailReason,
                SourceContextId, 0L, CastFlowId, ResolvedConfiguration,
                IsAborted, StartTime, _characterModeId, _characterModePriority,
                _characterModeOwnerId, _characterModeInstanceId, _hfsmParameterOwnerId,
                SummonInputIdentity, shared);
        }

        internal void RestoreRollbackState(in SkillPipelineContextRollbackState state)
        {
            if (state.ElapsedRaw < 0 || state.Frame < 0 || state.TargetActorId < 0 ||
                state.CastSequence < 0 || state.TimelineNextEventIndex < 0 ||
                state.SkillCooldownMs < 0 || state.CastFlowId < 0)
                throw new InvalidOperationException("Invalid skill pipeline context rollback state.");
            ReadPreparedParameterRollbackState(state.SharedData, out var preparedParameters,
                out var parametersCommitted, out var sharedData);
            _elapsedRaw = state.ElapsedRaw;
            SetFrame(state.Frame);
            SetCastSequence(state.CastSequence);
            SetTimelineNextEventIndex(state.TimelineNextEventIndex);
            SkillCooldownMs = state.SkillCooldownMs;
            FailReason = state.FailReason;
            SourceContextId = state.SourceContextId;
            SummonInputIdentity = state.SummonInputIdentity;
            DiagnosticCommandId = 0L; // Restored authority has no local diagnostic command association.
            CastFlowId = state.CastFlowId;
            ResolvedConfiguration = state.ResolvedConfiguration;
            var aimPos = state.AimPos;
            var aimDir = state.AimDir;
            UpdateInput(in aimPos, in aimDir, state.TargetActorId,
                hasAimPos: true, hasAimDir: true, hasTarget: true);
            InputReleased = state.InputReleased;
            IsAborted = state.IsAborted;
            StartTime = state.StartTime;
            _characterModeId = state.CharacterModeId;
            _characterModePriority = state.CharacterModePriority;
            _characterModeOwnerId = state.CharacterModeOwnerId;
            _characterModeInstanceId = state.CharacterModeInstanceId;
            _characterModeCleanupRegistered = false;
            if (!string.IsNullOrEmpty(_characterModeId)) RegisterCharacterModeCleanup();
            _hfsmParameterOwnerId = state.HfsmParameterOwnerId;
            _hfsmParameterCleanupRegistered = false;
            if (_hfsmParameterOwnerId > 0) RegisterHfsmParameterCleanup();
            this.SetSourceContextId(SourceContextId);
            SharedData.Clear();
            foreach (var item in sharedData) SharedData.Add(item.Key, item.Value);
            _preparedParameters = preparedParameters;
            _parametersCommitted = parametersCommitted;
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
            if (SummonInputIdentity.IsValid)
            {
                var inputIdentity = SummonInputIdentity;
                snapshot = snapshot.WithSummonInputIdentity(in inputIdentity);
            }
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

            ClearData();

            _disposables.Clear();
            _cleanupActions.Clear();
            _characterModeId = null;
            _characterModePriority = 0;
            _characterModeOwnerId = 0L;
            _characterModeInstanceId = 0L;
            _characterModeCleanupRegistered = false;

            RuntimeHandle = default;
            RuntimeId = 0L;
            SummonInputIdentity = default;
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
            PipelineContinuous = null;
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
            string characterModeId, int characterModePriority, long characterModeOwnerId,
            long characterModeInstanceId, long hfsmParameterOwnerId,
            MobaSummonInputIdentity summonInputIdentity,
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
            CharacterModeId = characterModeId;
            CharacterModePriority = characterModePriority;
            CharacterModeOwnerId = characterModeOwnerId;
            CharacterModeInstanceId = characterModeInstanceId;
            HfsmParameterOwnerId = hfsmParameterOwnerId;
            SummonInputIdentity = summonInputIdentity;
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
        public string CharacterModeId { get; }
        public int CharacterModePriority { get; }
        public long CharacterModeOwnerId { get; }
        public long CharacterModeInstanceId { get; }
        public long HfsmParameterOwnerId { get; }
        public MobaSummonInputIdentity SummonInputIdentity { get; }
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
        public readonly MobaSummonInputIdentity SummonInputIdentity;

        public readonly MobaPreparedParameterSnapshot[] ParameterSnapshots;
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
            IUnitFacade targetUnit,
            MobaPreparedParameterSnapshot[] parameterSnapshots = null,
            MobaSummonInputIdentity summonInputIdentity = default)
        {
            ParameterSnapshots = parameterSnapshots == null ? Array.Empty<MobaPreparedParameterSnapshot>() :
                (MobaPreparedParameterSnapshot[])parameterSnapshots.Clone();
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
            SummonInputIdentity = summonInputIdentity;
        }
    }
}
