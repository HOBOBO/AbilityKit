using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.HFSM.Definition;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleAuthoritativeWorldRecoveryPort
    {
        void Configure(
            in BattleStartPlan plan,
            BattleContext context,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch);

        bool TryImport(in GatewayStateSyncSnapshot snapshot);
        bool TryApplyAuthoritativeState(in GatewayStateSyncSnapshot snapshot);
        bool TryCommitAuthoritativeState(in GatewayStateSyncSnapshot snapshot);
        void ResetAfterReconnect();
    }

    internal sealed class BattleAuthoritativeWorldRecoveryPort
        : IBattleAuthoritativeWorldRecoveryPort
    {
        private readonly BattleSimulationRuntime _simulation;
        private readonly BattleSessionHandles _handles;
        private readonly HashSet<int> _managedActorIds = new HashSet<int>();
        private HashSet<int> _committedProjectionActorIds = new HashSet<int>();
        private IWorld _managedWorld;
        private CommittedActorProjection _committedProjection;
        private BattleStartPlan _plan;
        private BattleContext _context;
        private float _fixedDeltaSeconds;
        private Func<WorldId, int> _resolveIdealFrameLimit;
        private Func<bool> _shouldForceHashMismatch;

        internal BattleAuthoritativeWorldRecoveryPort(
            BattleSimulationRuntime simulation,
            BattleSessionHandles handles)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
        }

        public void Configure(
            in BattleStartPlan plan,
            BattleContext context,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch)
        {
            _plan = plan;
            ResetImportScope();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fixedDeltaSeconds = fixedDeltaSeconds;
            _resolveIdealFrameLimit = resolveIdealFrameLimit ?? throw new ArgumentNullException(nameof(resolveIdealFrameLimit));
            _shouldForceHashMismatch = shouldForceHashMismatch ?? throw new ArgumentNullException(nameof(shouldForceHashMismatch));
        }

        public bool TryImport(in GatewayStateSyncSnapshot snapshot)
        {
            if (_context == null || _handles.Session == null || !AdmitAuthoritativeFrame(snapshot.Frame)) return false;

            _simulation.StartRemoteDriven(
                _plan,
                _context,
                _fixedDeltaSeconds,
                _resolveIdealFrameLimit,
                _shouldForceHashMismatch);

            var world = _handles.RemoteDriven.World;
            var importer = _handles.RemoteDriven.Capabilities.StateImporter;
            if (world == null || importer == null)
            {
                Log.Warning(
                    "[BattleAuthoritativeWorldRecoveryPort] Remote-driven importer is unavailable.");
                return false;
            }

            var result = ImportAuthoritativeState(importer, world, in snapshot);
            if (result.Failed > 0)
            {
                Log.Warning(
                    $"[BattleAuthoritativeWorldRecoveryPort] Import incomplete. " +
                    $"frame={snapshot.Frame} failed={result.Failed}");
                ResetAfterReconnect();
                return false;
            }

            if (_plan.UsesStateSyncAuthority) return true;

            var runtime = _handles.RemoteDriven.Runtime;
            if (runtime == null ||
                !runtime.Features.TryGetFeature<IClientPredictionBaselineControl>(out var baseline) ||
                baseline == null ||
                !baseline.TryRebase(world.Id, new FrameIndex(snapshot.Frame)))
            {
                Log.Warning(
                    $"[BattleAuthoritativeWorldRecoveryPort] Prediction rebase failed. " +
                    $"frame={snapshot.Frame} worldId={world.Id.Value}");
                ResetAfterReconnect();
                return false;
            }

            _simulation.RemoteDrivenLastTickedFrame = snapshot.Frame;
            return true;
        }

        public bool TryApplyAuthoritativeState(in GatewayStateSyncSnapshot snapshot)
        {
            if (!AdmitAuthoritativeFrame(snapshot.Frame)) return false;
            var importer = _handles.RemoteDriven.Capabilities.StateImporter;
            if (importer == null)
            {
                Log.Warning(
                    $"[BattleAuthoritativeWorldRecoveryPort] Authoritative state apply skipped. " +
                    $"frame={snapshot.Frame}");
                return false;
            }

            var result = ImportAuthoritativeState(importer, _handles.RemoteDriven.World, in snapshot);
            if (result.Failed == 0) return true;

            Log.Warning(
                $"[BattleAuthoritativeWorldRecoveryPort] Authoritative state apply incomplete. " +
                $"frame={snapshot.Frame} failed={result.Failed}");
            ResetAfterReconnect();
            return false;
        }

        private bool AdmitAuthoritativeFrame(int frame)
        {
            if (_plan.UsesStateSyncAuthority) return true;

            var world = _handles.RemoteDriven.World;
            var runtime = _handles.RemoteDriven.Runtime;
            if (world == null || runtime == null ||
                !runtime.Features.TryGetFeature<IClientPredictionRestoreFloor>(out var boundary) || boundary == null ||
                !boundary.TryGetRestoreFloor(world.Id, out var floor) || frame >= floor.Value)
                return true;

            Log.Warning($"[BattleAuthoritativeWorldRecoveryPort] Rejected state below restore floor. frame={frame} floor={floor.Value}");
            return false;
        }

        /// <summary>
        /// 逻辑导入和快照基线提交均成功后，才发布状态同步世界的权威 Tick。
        /// </summary>
        public bool TryCommitAuthoritativeState(in GatewayStateSyncSnapshot snapshot)
        {
            if (!_plan.UsesStateSyncAuthority) return true;
            if (!StateSyncWorldRuntime.TryCommitTick(_handles.RemoteDriven.World, snapshot.Frame, _fixedDeltaSeconds))
                return false;

            _committedProjectionActorIds.Clear();
            if (UsesScopedLogicImport(in _plan))
                _committedProjectionActorIds.UnionWith(_managedActorIds);
            else if (snapshot.Actors != null)
                foreach (var actor in snapshot.Actors)
                    _committedProjectionActorIds.Add(actor.ActorId);
            _simulation.RemoteDrivenLastTickedFrame = snapshot.Frame;
            return true;
        }

        internal IActorProjectionProducer ResolveCommittedProjection()
        {
            if (!_plan.UsesStateSyncAuthority || _handles.RemoteDriven.Capabilities.ProjectionProducer is not { } producer)
                return null;
            if (_committedProjection == null || !ReferenceEquals(_committedProjection.Source, producer))
                _committedProjection = new CommittedActorProjection(producer, _committedProjectionActorIds);
            return _committedProjection;
        }

        /// <summary>
        /// 权威快照控制对照视图的可见名单，字段仍从导入后的世界提取。
        /// 已排队退场但尚未物理清理的 Actor 不会留在对照轨。
        /// </summary>
        private sealed class CommittedActorProjection : IActorProjectionProducer
        {
            private readonly HashSet<int> _committedIds;
            internal IActorProjectionProducer Source { get; }

            internal CommittedActorProjection(IActorProjectionProducer source, HashSet<int> committedIds)
            {
                Source = source;
                _committedIds = committedIds;
            }

            public ActorProjectionData ExtractFull(int actorId) =>
                _committedIds.Contains(actorId) ? Source.ExtractFull(actorId) : default;

            public ActorProjectionData ExtractSpawn(int actorId) =>
                _committedIds.Contains(actorId) ? Source.ExtractSpawn(actorId) : default;

            public void ExtractAll(List<ActorProjectionData> buffer)
            {
                if (buffer == null) return;
                Source.ExtractAll(buffer);
                buffer.RemoveAll(IsOutsideCommittedScope);
            }

            private bool IsOutsideCommittedScope(ActorProjectionData actor) => !_committedIds.Contains(actor.ActorId);
        }

        internal bool MatchesLocalCastActor(in LocalStateSyncCastScope scope)
        {
            var services = _handles.RemoteDriven.World?.Services;
            return _plan.UsesStateSyncAuthority && services != null &&
                services.TryResolve<MobaActorRegistry>(out var actors) && actors.TryGet(scope.ActorId, out _) &&
                actors.GetEntityVersion(scope.ActorId) == scope.EntityVersion;
        }

        public void ResetAfterReconnect()
        {
            ResetImportScope();
            _simulation.DisposeRemoteDrivenWorld();
            if (_plan.UsesStateSyncAuthority) return;

            var reconcile = _context?.PredictionReconcileControl;
            if (reconcile == null) return;

            var worldId = _context.HasRuntimeWorldId
                ? _context.RuntimeWorldId
                : new WorldId(_plan.World.WorldId);
            if (string.IsNullOrWhiteSpace(worldId.Value)) return;

            reconcile.ResetReconcile(worldId);
            reconcile.SetReconcileEnabled(worldId, true);
        }

        private void ResetImportScope()
        {
            _managedWorld = null;
            _managedActorIds.Clear();
            _committedProjectionActorIds.Clear();
            _committedProjectionActorIds = new HashSet<int>();
            _committedProjection = null;
        }

        internal static bool UsesScopedLogicImport(in BattleStartPlan plan) =>
            string.Equals(plan.RemoteSyncCapabilities?.ProfileName, BattleReplicationRuntime.SyncProfileName,
                StringComparison.OrdinalIgnoreCase);

        internal static List<MobaActorStateImport> CreateLogicImports(
            IReadOnlyList<GatewayStateSyncActorSnapshot> actors, bool scoped)
        {
            var imports = new List<MobaActorStateImport>(actors.Count);
            for (var index = 0; index < actors.Count; index++)
            {
                var actor = actors[index];
                if (scoped && (actor.Kind == (int)SpawnEntityKind.Minion ||
                               actor.Kind == (int)SpawnEntityKind.Summon ||
                               actor.Kind == (int)SpawnEntityKind.Building)) continue;

                imports.Add(new MobaActorStateImport(
                    actor.ActorId, actor.X, actor.Y, actor.Z, actor.Rotation, actor.Hp, actor.HpMax,
                    actor.TeamId, actor.Kind, actor.Code, actor.OwnerNetId));
            }

            return imports;
        }

        private MobaStateImportResult ImportAuthoritativeState(
            MobaLogicWorldStateImporter importer,
            IWorld world,
            in GatewayStateSyncSnapshot snapshot)
        {
            var scoped = UsesScopedLogicImport(in _plan);
            if (scoped && !ReferenceEquals(world, _managedWorld))
            {
                ResetImportScope();
                _managedWorld = world;
            }

            var imports = CreateLogicImports(snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>(), scoped);
            var requiresHfsm = snapshot.SchemaVersion >= 2 ||
                snapshot.PayloadOpCode == MobaCharacterHfsmRollbackProvider.DefaultKey ||
                snapshot.PayloadOpCode == MobaCharacterHfsmWireCodec.CompressedOpCode;
            MobaActorRegistry registry = null;
            StateMachineDefinition definition = null;
            if (world?.Services != null)
            {
                world.Services.TryResolve(out registry);
                world.Services.TryResolve(out definition);
            }
            var result = new MobaStateImportResult();
            if (requiresHfsm && registry == null)
            {
                Log.Warning("[BattleAuthoritativeWorldRecoveryPort] Character HFSM baseline is missing.");
                result.Failed++;
                return result;
            }

            try
            {
                if (requiresHfsm && !CharacterHfsmGatewayStateImporter.TryApply(
                        in snapshot, new MobaActorRegistry(), definition))
                {
                    result.Failed++;
                    return result;
                }
                result = scoped
                    ? importer.ImportScopedFullSnapshot(imports, snapshot.Frame, _managedActorIds)
                    : importer.Import(imports, snapshot.Frame, isFullSnapshot: true);
                if (result.Failed == 0 && registry != null &&
                    !CharacterHfsmGatewayStateImporter.TryApply(in snapshot, registry, definition))
                    result.Failed++;

                if (scoped && result.Failed == 0)
                {
                    _managedActorIds.Clear();
                    for (var index = 0; index < imports.Count; index++)
                        _managedActorIds.Add(imports[index].ActorId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[BattleAuthoritativeWorldRecoveryPort] State import failed: " + ex.Message);
                result.Failed++;
            }
            return result;
        }
    }

    /// <summary>
    /// 负责单个远端复制代际的权威快照准入、断线恢复与全量状态请求。
    /// 项目恢复动作通过通用协调器和动作路由执行，避免各信号来源重复维护策略分支。
    /// </summary>
    internal sealed class AuthoritativeStateRecoveryRuntime :
        IDisposable,
        INetworkSessionRecoverySignalSink
    {
        private const string RecoveryCorrelationPrefix = "moba-recovery-generation:";
        private readonly RecoveryGenerationOperation _operation = new RecoveryGenerationOperation();
        private readonly BattleReplicationRuntime _replication;
        private readonly ReliableBattleEventDeliveryRuntime _reliableEvents;
        private readonly IBattleAuthoritativeWorldRecoveryPort _worldRecovery;
        private readonly NetworkSessionRecoveryCoordinator _sessionRecovery;
        private readonly NetworkSessionRecoveryActionRouter<bool> _recoveryActions;
        private IBattleRecoveryTransportOperations _transport;
        private BattleContext _context;
        private long _remoteInterpolationGeneration;
        private Action _firstFrameReceived;
        private long _lastReliableEventResyncWindowId = -1L;
        private long _reliableEventResyncSuppressedCount;
        private bool _usesMovementProof;
        private ILocalStateSyncCastSession _localCast;

        /// <summary>
        /// 当前已提交固定基线截面附带的证明。恢复中或当前截面缺少证明时为空。
        /// </summary>
        internal GatewayStateSyncMovementProof? CommittedMovementProof { get; private set; }

        internal AuthoritativeStateRecoveryRuntime(
            BattleReplicationRuntime replication,
            ReliableBattleEventDeliveryRuntime reliableEvents,
            IBattleAuthoritativeWorldRecoveryPort worldRecovery)
        {
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _reliableEvents = reliableEvents ?? throw new ArgumentNullException(nameof(reliableEvents));
            _worldRecovery = worldRecovery ?? throw new ArgumentNullException(nameof(worldRecovery));
            _sessionRecovery = new NetworkSessionRecoveryCoordinator(
                new NetworkSessionRecoveryOptions
                {
                    // MOBA 的恢复信号已经由状态准入和可靠事件游标去重，保留每次失败的重试机会。
                    DuplicateSignalWindow = TimeSpan.Zero,
                    AllowEqualPriorityReplacement = true
                });
            _recoveryActions = new NetworkSessionRecoveryActionRouter<bool>(
                    new NetworkSessionRecoveryActionRouterOptions<bool>
                    {
                        UnhandledActionPolicy = NetworkSessionRecoveryUnhandledActionPolicy.ReturnUnhandled,
                        HandlerFailurePolicy = NetworkSessionRecoveryHandlerFailurePolicy.CaptureAndReturn
                    })
                .Register(
                    NetworkSessionRecoveryAction.WaitForReconnect,
                    (_, _) => Task.FromResult(true))
                .Register(
                    NetworkSessionRecoveryAction.RequestFullSnapshot,
                    ExecuteFullStateRecoveryAsync)
                .Register(
                    NetworkSessionRecoveryAction.RestoreReliableEventBaseline,
                    ExecuteFullStateRecoveryAsync);
        }

        internal bool PendingStateImport => _replication.PendingStateImport;
        internal IActorProjectionProducer ResolveCommittedProjection() =>
            (_worldRecovery as BattleAuthoritativeWorldRecoveryPort)?.ResolveCommittedProjection();
        internal Task PendingExecution => _operation.PendingExecution;
        internal NetworkSessionRecoveryDecision RecoveryDecision =>
            _sessionRecovery.CurrentDecision;
        internal NetworkSessionRecoveryDiagnostics RecoveryDiagnostics =>
            _sessionRecovery.GetDiagnostics();
        internal long ReliableEventResyncSuppressedCount =>
            _reliableEventResyncSuppressedCount;

        public bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            return _sessionRecovery.TryReport(in signal, out decision);
        }

        internal Task<NetworkSessionRecoveryExecutionResult<bool>> ExecuteRecoveryDecisionAsync(
            NetworkSessionRecoveryDecision decision)
        {
            return _recoveryActions.ExecuteAsync(decision);
        }

        /// <summary>
        /// 在逻辑会话开始连接前绑定权威快照、重连与可靠事件入口；帧同步不进入该路径。
        /// </summary>
        internal void AttachStateSyncTransport(AbilityKit.Network.Battle.NetworkTransport transport,
            ulong worldId, IMobaReliableBattleEventCheckpointStore checkpointStore, BattleContext context,
            in BattleStartPlan plan, float fixedDeltaSeconds, Action<WireReliableBattleEvent> eventSink,
            Action firstFrameReceived)
        {
            if (!plan.UsesStateSyncAuthority || worldId == 0)
                throw new ArgumentException("A state-authority plan with a numeric world id is required.", nameof(plan));

            var checkpoint = plan.ReliableEventCheckpoint;
            if (!_replication.Build(transport, plan.World.TickRate, worldId, plan.Gateway.BattleId ?? string.Empty,
                in checkpoint, HandleSnapshot, HandleReliableEvents, HandleConnectionClosed,
                HandleConnectionEstablished, HandleAuthenticationFailed, plan.RemoteSyncCapabilities))
                Log.Warning("[AuthoritativeStateRecoveryRuntime] Reliable event checkpoint rejected for this battle.");

            try
            {
                BeginGeneration(new NetworkBattleRecoveryTransportOperations(transport), checkpointStore, context,
                    in plan, fixedDeltaSeconds, _ => 0, () => false, eventSink, firstFrameReceived);
            }
            catch
            {
                EndGeneration();
                _replication.Dispose();
                throw;
            }
        }

        internal void BeginGeneration(
            IBattleRecoveryTransportOperations transport,
            IMobaReliableBattleEventCheckpointStore checkpointStore,
            BattleContext context,
            in BattleStartPlan plan,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch,
            Action<WireReliableBattleEvent> eventSink,
            Action firstFrameReceived)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_replication.ReliableEventCursor == null)
                throw new InvalidOperationException("Replication runtime must be built before recovery.");

            EndGeneration();
            _lastReliableEventResyncWindowId = -1L;
            _reliableEventResyncSuppressedCount = 0L;
            _operation.Begin();

            try
            {
                _transport = transport;
                _context = context;
                _firstFrameReceived = firstFrameReceived;
                _usesMovementProof = plan.UsesStateSyncAuthority && plan.Gateway.UseScopedStateSyncInput;
                _worldRecovery.Configure(
                    in plan,
                    context,
                    fixedDeltaSeconds,
                    resolveIdealFrameLimit,
                    shouldForceHashMismatch);
                _reliableEvents.BeginGeneration(
                    _replication.SyncSession,
                    _replication.ReliableEventCursor,
                    transport,
                    checkpointStore,
                    eventSink,
                    HandleReliableTimelineInvalidated);
                _replication.PendingStateImport = true;
                _operation.ConnectionRecoveryPending = false;
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                _remoteInterpolationGeneration = context.BeginRemoteInterpolation();
            }
            catch
            {
                try
                {
                    EndGeneration();
                }
                catch
                {
                    // 保留最初的初始化异常；下方仍会把当前持有者恢复到不可推进状态。
                }

                _replication.PendingStateImport = true;
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                throw;
            }
        }

        internal void HandleSnapshot(object rawSnapshot) => HandleSnapshotCore(rawSnapshot, null);

        /// <summary>
        /// 离线投递时捕获的客户端会话代际，不能用快照 ID 或输入终结序号代替。
        /// </summary>
        internal int LocalReplayGeneration => _operation.Generation;

        /// <summary>
        /// 离线配方使用真实快照提交路径。返回值仅表示本次是否提交成功，
        /// 成功提交也可能没有输入证明；不从缓存推断本次结果。
        /// </summary>
        internal bool TryCommitLocalReplaySnapshot(int deliveryGeneration, in GatewayStateSyncSnapshot snapshot,
            out GatewayStateSyncMovementProof? proof)
        {
            proof = null;
            if (!_operation.IsCurrent(deliveryGeneration)) return false;

            var committed = false;
            GatewayStateSyncMovementProof? observedProof = null;
            HandleSnapshotCore(snapshot, (_, value) =>
            {
                committed = true;
                observedProof = value;
            });
            if (!committed || !_operation.IsCurrent(deliveryGeneration)) return false;
            proof = observedProof;
            return true;
        }

        /// <summary>
        /// 仅显式离线验收装配施法域；由当前恢复代际负责释放。
        /// </summary>
        internal LocalStateSyncCastSession<TAnchor, TObservation> AttachLocalCast<TAnchor, TObservation>(
            in LocalStateSyncCastScope scope, Func<ILocalStateSyncCastWorld<TAnchor, TObservation>> create,
            bool movementEnabled = false, int projectileMask = 0)
        {
            if (!_operation.IsCurrent(scope.Generation) || _localCast != null ||
                _replication.Transport?.Options.StateSyncInputGeneration != scope.InputGeneration ||
                _worldRecovery is not BattleAuthoritativeWorldRecoveryPort)
                throw new InvalidOperationException("本地施法必须装配到当前独立的状态同步代际。");
            var session = new LocalStateSyncCastSession<TAnchor, TObservation>(in scope, create, movementEnabled, projectileMask);
            _localCast = session;
            return session;
        }

        /// <summary>
        /// 显式离线装配正式战斗候选，所有权交给当前恢复代际的施法会话。
        /// </summary>
        internal LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation> AttachLocalBattleCast(
            in LocalStateSyncCastScope scope, float fixedDelta, Func<EntitasWorld> createWorld,
            bool observeCastProgress = false, bool movementEnabled = false, int projectileMask = 0, bool observeActorPose = false)
        {
            var identity = scope;
            return AttachLocalCast<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(in scope,
                () => new LocalStateSyncBattleCastWorld(in identity, fixedDelta, createWorld, observeCastProgress, projectileMask, observeActorPose),
                movementEnabled, projectileMask);
        }

        /// <summary>
        /// 先提交权威角色；只有同次提交的施法覆盖和终结证明才能进入恢复。
        /// 缺失或错配的覆盖只采用权威观测，不用随附证明裁剪输入。
        /// </summary>
        internal LocalStateSyncCastResult CommitLocalBattleCastSnapshot(
            LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation> session,
            in GatewayStateSyncSnapshot snapshot, in LocalStateSyncCastStamp stamp, in LocalStateSyncCastEconomy economy,
            LocalStateSyncCastTerminalProof? terminal, in LocalStateSyncBattleCastCoverage coverage,
            LocalStateSyncMovementCut? movement = null, LocalStateSyncProjectileCut projectile = null)
        {
            if (!TryCommitLocalCastActor(session, in snapshot, in stamp, out var actor, out var materialized))
                return LocalStateSyncCastResult.Ignored;

            var state = new MobaActorStateImport(actor.ActorId, actor.X, actor.Y, actor.Z, actor.Rotation,
                actor.Hp, actor.HpMax, actor.TeamId, actor.Kind, actor.Code, actor.OwnerNetId);
            var castCovered = coverage.Matches(in stamp);
            var movementCovered = session.MovementEnabled
                ? movement is { } motion && motion.Matches(in stamp, in state)
                : !movement.HasValue;
            var windup = castCovered ? coverage.Windup : null;
            var projectileCovered = session.ProjectileMask == 0 ? projectile == null :
                projectile != null && windup == null && !session.MovementEnabled &&
                projectile.Matches(in stamp, session.ProjectileMask, materialized.Actors);
            var cut = new LocalStateSyncBattleCastCut(stamp.Tick, state, windup?.Economy ?? economy, windup,
                movementCovered ? movement : null, projectileCovered ? projectile : null);
            var anchor = new LocalStateSyncCastAnchor<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
                in stamp, cut, cut.Observe());
            return session.Adopt(in anchor, castCovered && movementCovered && projectileCovered ? terminal : null);
        }

        /// <summary>
        /// 在真实角色快照提交后才处理施法截面，终结证明独立于移动。
        /// </summary>
        internal LocalStateSyncCastResult CommitLocalCastSnapshot<TAnchor, TObservation>(
            LocalStateSyncCastSession<TAnchor, TObservation> session, in GatewayStateSyncSnapshot snapshot,
            in LocalStateSyncCastAnchor<TAnchor, TObservation> anchor, LocalStateSyncCastTerminalProof? terminal)
        {
            return TryCommitLocalCastActor(session, in snapshot, in anchor.Stamp, out _, out _)
                ? session.Adopt(in anchor, terminal) : LocalStateSyncCastResult.Ignored;
        }

        private bool TryCommitLocalCastActor<TAnchor, TObservation>(
            LocalStateSyncCastSession<TAnchor, TObservation> session, in GatewayStateSyncSnapshot snapshot,
            in LocalStateSyncCastStamp stamp, out GatewayStateSyncActorSnapshot actor, out GatewayStateSyncSnapshot materialized)
        {
            actor = default;
            materialized = default;
            var scope = session.Scope;
            if (!ReferenceEquals(session, _localCast) || !_operation.IsCurrent(scope.Generation) ||
                _operation.ConnectionRecoveryPending)
                return false;

            GatewayStateSyncSnapshot? committed = null;
            HandleSnapshotCore(snapshot, (state, _) => committed = state);
            if (committed is not { } committedState || !ReferenceEquals(session, _localCast) ||
                !_operation.IsCurrent(scope.Generation) || _operation.ConnectionRecoveryPending ||
                _replication.PendingStateImport)
                return false;
            materialized = committedState;

            if (!snapshot.UsesFixedBaseline || snapshot.WorldId != scope.WorldId || snapshot.StateStreamGeneration != scope.Stream ||
                !TryGetLocalCastOwner(in materialized, in scope, out actor) ||
                !((BattleAuthoritativeWorldRecoveryPort)_worldRecovery).MatchesLocalCastActor(in scope))
            {
                _localCast = null;
                session.Dispose();
                return false;
            }

            if (stamp.Revision != snapshot.SnapshotId || stamp.Tick != materialized.Frame || !scope.Matches(in stamp.Scope))
            {
                session.Suspend();
                return false;
            }
            return true;
        }

        private static bool TryGetLocalCastOwner(in GatewayStateSyncSnapshot snapshot, in LocalStateSyncCastScope scope,
            out GatewayStateSyncActorSnapshot actor)
        {
            if (snapshot.Actors != null)
                foreach (var entry in snapshot.Actors)
                    if (entry.ActorId == scope.ActorId && entry.OwnerNetId > 0 && (ulong)entry.OwnerNetId == scope.PlayerId)
                    {
                        actor = entry;
                        return true;
                    }
            actor = default;
            return false;
        }

        private void HandleSnapshotCore(object rawSnapshot, Action<GatewayStateSyncSnapshot, GatewayStateSyncMovementProof?> observeCommitted)
        {
            var snapshotAdmission = _replication.SnapshotAdmission;
            if (rawSnapshot is not GatewayStateSyncSnapshot snapshot ||
                snapshotAdmission == null)
            {
                return;
            }

            var authoritativeState = _replication.AuthoritativeSnapshotState;
            var generation = _operation.Generation;
            GatewayStateSyncSnapshot fixedMaterialized = default;
            if (snapshot.UsesFixedBaseline)
            {
                if (!snapshotAdmission.IsExpectedWorld(snapshot.WorldId) || authoritativeState == null)
                    return;
                var status = authoritativeState.PrepareFixed(in snapshot, out fixedMaterialized);
                if (status != MobaSnapshotAdmissionStatus.Accepted)
                {
                    if (status == MobaSnapshotAdmissionStatus.BaselineRequired)
                    {
                        RequireFullStateBaseline();
                        ReportAndExecute(NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                            SyncHealthSeverity.Error, "snapshot-fixed-baseline-mismatch",
                            snapshotAdmission.LastAcceptedFrame);
                    }
                    return;
                }
            }
            else if (authoritativeState?.HasFixedStream == true)
            {
                return;
            }

            var admission = snapshot.UsesFixedBaseline
                ? snapshotAdmission.CheckFixed(in snapshot)
                : snapshotAdmission.Admit(snapshot.WorldId, snapshot.Frame, snapshot.IsFullSnapshot,
                    MobaSnapshotAdmission.ResolveAdmissionSchema(in snapshot));
            if (!admission.Accepted)
            {
                Log.Warning(
                    $"[AuthoritativeStateRecoveryRuntime] Snapshot rejected. " +
                    $"status={admission.Status} worldId={snapshot.WorldId} frame={snapshot.Frame}");
                if (admission.ShouldRequestFullResync)
                {
                    RequireFullStateBaseline();
                    ReportAndExecute(
                        NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                        SyncHealthSeverity.Error,
                        $"snapshot-admission:{admission.Status}",
                        admission.LastAcceptedFrame);
                }
                return;
            }

            var pendingStateImport = _replication.PendingStateImport;
            if (pendingStateImport)
            {
                if (!snapshot.IsFullSnapshot ||
                    !_worldRecovery.TryImport(in snapshot) ||
                    !_reliableEvents.AdoptAuthoritativeBaseline(
                        snapshot.EventEpoch,
                        snapshot.EventWatermark))
                {
                    _worldRecovery.ResetAfterReconnect();
                    FailSnapshotApplication(snapshot.Frame, "state-import-failed");
                    return;
                }
            }

            var materialized = snapshot.UsesFixedBaseline
                ? fixedMaterialized
                : authoritativeState?.Apply(in snapshot) ?? snapshot;
            if (!pendingStateImport && !_worldRecovery.TryApplyAuthoritativeState(in materialized))
            {
                FailSnapshotApplication(snapshot.Frame, "state-apply-failed");
                return;
            }

            if (snapshot.UsesFixedBaseline)
            {
                if (!_operation.IsCurrent(generation)) return;
                if (!snapshotAdmission.CommitFixed(in snapshot).Accepted)
                {
                    _worldRecovery.ResetAfterReconnect();
                    FailSnapshotApplication(snapshot.Frame, "state-admission-changed");
                    return;
                }

                authoritativeState?.CommitFixed(in snapshot, in materialized);
            }
            if (!_operation.IsCurrent(generation)) return;
            if (!_worldRecovery.TryCommitAuthoritativeState(in materialized))
            {
                _worldRecovery.ResetAfterReconnect();
                FailSnapshotApplication(snapshot.Frame, "state-tick-commit-failed");
                return;
            }

            if (!_operation.IsCurrent(generation)) return;
            CommittedMovementProof = _usesMovementProof && !_operation.ConnectionRecoveryPending &&
                snapshot.MovementProof is { } proof && proof.Matches(in snapshot) &&
                ContainsMovementActor(in materialized, proof.ActorId) ? proof : null;
            if (observeCommitted != null && _operation.IsCurrent(generation))
                observeCommitted(materialized, CommittedMovementProof);

            if (pendingStateImport)
                FinishStateImport(snapshot.Frame);

            _firstFrameReceived?.Invoke();
            var sample = new MobaRemoteSnapshotSample(
                materialized.WorldId,
                materialized.Frame,
                materialized.Actors);
            _replication.ReplicationPipeline?.ObserveRemote(in sample);
            if (_operation.IsCurrent(generation) && snapshot.PresentationCueBaseline is { Length: > 0 } cueBaseline &&
                _context?.FrameSnapshots is { } presentation)
            {
                var worldId = _context.HasRuntimeWorldId ? _context.RuntimeWorldId : default;
                var cueSnapshot = new WorldStateSnapshot(MobaOpCodes.Snapshot.PresentationCue, cueBaseline);
                presentation.Feed(new FramePacket(worldId, new FrameIndex(snapshot.Frame), null, cueSnapshot));
            }
            if (_operation.IsCurrent(generation) && _context?.FrameSnapshots is { } overheadPresentation)
            {
                var worldId = _context.HasRuntimeWorldId ? _context.RuntimeWorldId : default;
                if (snapshot.ShieldStateBaseline is { Length: > 0 } shields)
                    overheadPresentation.Feed(new FramePacket(worldId, new FrameIndex(snapshot.Frame), null,
                        new WorldStateSnapshot(MobaOpCodes.Snapshot.ShieldState, shields)));
                if (snapshot.ActorIndicatorBaseline is { Length: > 0 } indicators)
                    overheadPresentation.Feed(new FramePacket(worldId, new FrameIndex(snapshot.Frame), null,
                        new WorldStateSnapshot(MobaOpCodes.Snapshot.ActorIndicatorState, indicators)));
            }
        }

        private static bool ContainsMovementActor(in GatewayStateSyncSnapshot snapshot, int actorId)
        {
            if (snapshot.Actors == null) return false;
            foreach (var actor in snapshot.Actors)
                if (actor.ActorId == actorId) return true;
            return false;
        }

        private void FinishStateImport(int frame)
        {
            _replication.PendingStateImport = false;
            _context?.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, false);
            var recovered = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.Recovered,
                SyncHealthSeverity.Info,
                frame,
                correlationContext: CreateRecoveryCorrelationContext(_operation.Generation),
                detail: "authoritative-baseline-adopted");
            _sessionRecovery.TryReport(in recovered, out _);
        }

        private void RequireFullStateBaseline()
        {
            CommittedMovementProof = null;
            _replication.PendingStateImport = true;
            _replication.SnapshotAdmission?.RequireFullBaseline();
            _localCast?.Suspend();
            _context?.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
        }

        private void FailSnapshotApplication(int frame, string reason)
        {
            RequireFullStateBaseline();
            ReportAndExecute(NetworkSessionRecoverySignalKind.SnapshotResyncRequired, SyncHealthSeverity.Error,
                reason, frame);
        }

        internal void HandleReliableEvents(object rawPush)
        {
            if (rawPush is WireReliableBattleEventPush push)
            {
                _reliableEvents.Handle(in push);
            }
        }

        internal void HandleConnectionClosed()
        {
            if (!_replication.IsBuilt) return;
            CommittedMovementProof = null;
            _operation.ConnectionRecoveryPending = true;
            _localCast?.Suspend();
            Log.Warning(
                "[AuthoritativeStateRecoveryRuntime] Battle connection lost; " +
                "transport recovery is pending.");
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ConnectionLost,
                SyncHealthSeverity.Warning,
                "battle-connection-closed",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
        }

        internal void HandleConnectionEstablished()
        {
            if (!_replication.IsBuilt || !_operation.ConnectionRecoveryPending) return;
            _operation.ConnectionRecoveryPending = false;
            RequireFullStateBaseline();
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ConnectionRestored,
                SyncHealthSeverity.Info,
                "battle-connection-established",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
            _worldRecovery.ResetAfterReconnect();
            _replication.LastServerAckFrame = 0;
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                SyncHealthSeverity.Error,
                "connection-re-established",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
        }

        internal void HandleAuthenticationFailed(Exception exception)
        {
            if (!_replication.IsBuilt || _transport == null) return;
            Log.Warning(
                $"[AuthoritativeStateRecoveryRuntime] Authentication failed: {exception?.Message}. " +
                "Disconnecting for transport re-authentication.");
            _operation.ConnectionRecoveryPending = true;
            _localCast?.Suspend();
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ConnectionError,
                SyncHealthSeverity.Error,
                "battle-authentication-failed",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0,
                exception);
            try
            {
                _transport.Disconnect();
            }
            catch (Exception disconnectException)
            {
                Log.Exception(
                    disconnectException,
                    "[AuthoritativeStateRecoveryRuntime] Disconnect after authentication failure failed.");
            }
        }

        public void Dispose()
        {
            EndGeneration();
        }

        internal Task StopAsync()
        {
            return EndGenerationAsync();
        }

        internal void HandleReliableTimelineInvalidated(string reason)
        {
            var recoveryWindowId = _reliableEvents.RecoveryWindowId;
            if (recoveryWindowId > 0L &&
                recoveryWindowId == _lastReliableEventResyncWindowId)
            {
                _reliableEventResyncSuppressedCount++;
                return;
            }

            if (!ReportAndExecute(
                NetworkSessionRecoverySignalKind.ReliableEventResyncRequired,
                SyncHealthSeverity.Error,
                reason,
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0))
            {
                return;
            }

            if (recoveryWindowId > 0L)
                _lastReliableEventResyncWindowId = recoveryWindowId;
        }

        private async Task<bool> ExecuteFullStateRecoveryAsync(
            NetworkSessionRecoveryExecutionContext execution,
            System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = _operation.Generation;
            var transport = _transport;
            if (transport == null || !IsCurrentRecoveryDecision(execution.Decision, generation))
            {
                return false;
            }

            RequireFullStateBaseline();
            _replication.AuthoritativeSnapshotState?.ResetForRecovery();
            _replication.InterpolationController?.Reset();
            _reliableEvents.RequireAuthoritativeBaseline();

            var signal = execution.Decision.Signal;
            var reason = string.IsNullOrWhiteSpace(signal.Detail)
                ? execution.Decision.Reason
                : signal.Detail;
            return await RequestFullStateSyncAsync(
                generation,
                transport,
                reason,
                signal.Frame);
        }

        private async Task<bool> RequestFullStateSyncAsync(
            int generation,
            IBattleRecoveryTransportOperations transport,
            string reason,
            int lastAuthoritativeFrame)
        {
            bool accepted;
            try
            {
                accepted = await transport.RequestFullStateSyncAsync(
                    reason,
                    lastAuthoritativeFrame);
            }
            catch (Exception ex)
            {
                if (!_operation.IsCurrent(generation) || !ReferenceEquals(transport, _transport)) return false;
                Log.Exception(
                    ex,
                    $"[AuthoritativeStateRecoveryRuntime] Full-state request failed. " +
                    $"reason={reason} frame={lastAuthoritativeFrame}");
                return false;
            }

            if (!_operation.IsCurrent(generation) || !ReferenceEquals(transport, _transport)) return false;
            if (!accepted)
            {
                Log.Warning(
                    $"[AuthoritativeStateRecoveryRuntime] Full-state request not accepted. " +
                    $"reason={reason} frame={lastAuthoritativeFrame}");
            }

            return accepted;
        }

        private bool ReportAndExecute(
            NetworkSessionRecoverySignalKind kind,
            SyncHealthSeverity severity,
            string detail,
            int frame,
                Exception exception = null)
        {
            var signal = new NetworkSessionRecoverySignal(
                kind,
                severity,
                frame,
                exception,
                CreateRecoveryCorrelationContext(_operation.Generation),
                detail: detail);
            if (!_sessionRecovery.TryReport(in signal, out var decision)) return false;

            if (!_operation.TryReserve(out var token, out var reservation)) return true;

            _ = ExecuteAndCompleteReservationAsync(decision, token, reservation);
            return true;
        }

        private static string CreateRecoveryCorrelationContext(int generation)
        {
            return $"{RecoveryCorrelationPrefix}{generation}";
        }

        private static bool IsCurrentRecoveryDecision(
            NetworkSessionRecoveryDecision decision,
            int generation)
        {
            var correlation = decision.Signal.CorrelationContext;
            if (string.IsNullOrWhiteSpace(correlation) ||
                !correlation.StartsWith(RecoveryCorrelationPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                correlation,
                CreateRecoveryCorrelationContext(generation),
                StringComparison.Ordinal);
        }

        private async Task ExecuteAndCompleteReservationAsync(
            NetworkSessionRecoveryDecision decision,
            System.Threading.CancellationToken token,
            TaskCompletionSource<bool> reservation)
        {
            try
            {
                var result = await _recoveryActions.ExecuteAsync(
                        decision,
                        cancellationToken: token)
                    .ConfigureAwait(false);
                if (result.Status == NetworkSessionRecoveryExecutionStatus.Unhandled)
                {
                    Log.Warning(
                        $"[AuthoritativeStateRecoveryRuntime] Recovery action is not registered. " +
                        $"action={result.Decision.Action} signal={result.Decision.Signal.Kind}");
                }
                else if (result.Status == NetworkSessionRecoveryExecutionStatus.Failed &&
                         result.Exception != null)
                {
                    Log.Exception(
                        result.Exception,
                        $"[AuthoritativeStateRecoveryRuntime] Recovery action failed. " +
                        $"action={result.Decision.Action} signal={result.Decision.Signal.Kind}");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Log.Exception(
                    exception,
                    "[AuthoritativeStateRecoveryRuntime] Failed to observe recovery action.");
            }
            finally
            {
                reservation.TrySetResult(true);
            }
        }

        private Task _pendingTeardown = Task.CompletedTask;

        private void EndGeneration()
        {
            _ = ObserveEndGenerationAsync(EndGenerationAsync());
        }

        private static async Task ObserveEndGenerationAsync(Task completion)
        {
            try { await completion.ConfigureAwait(false); }
            catch (Exception exception)
            {
                Log.Exception(exception,
                    "[AuthoritativeStateRecoveryRuntime] Recovery teardown failed.");
            }
        }

        private Task EndGenerationAsync()
        {
            CommittedMovementProof = null;
            var localCast = _localCast;
            _localCast = null;
            _usesMovementProof = false;
            var pendingExecution = _operation.EndAsync();

            var context = _context;
            var remoteInterpolationGeneration = _remoteInterpolationGeneration;
            _transport = null;
            _context = null;
            _remoteInterpolationGeneration = 0;
            _firstFrameReceived = null;
            _sessionRecovery.Reset();

            Exception cleanupFailure = null;
            try { localCast?.Dispose(); }
            catch (Exception exception)
            {
                cleanupFailure = cleanupFailure == null ? exception : new AggregateException(cleanupFailure, exception);
            }
            try
            {
                _reliableEvents.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure = cleanupFailure == null ? exception : new AggregateException(cleanupFailure, exception);
            }

            try
            {
                if (context != null && remoteInterpolationGeneration != 0)
                {
                    context.EndRemoteInterpolation(remoteInterpolationGeneration);
                }
            }
            catch (Exception exception)
            {
                cleanupFailure = cleanupFailure == null
                    ? exception
                    : new AggregateException(cleanupFailure, exception);
            }

            return _pendingTeardown = Task.WhenAll(
                _pendingTeardown,
                CompleteEndGenerationAsync(pendingExecution, cleanupFailure));
        }

        private static async Task CompleteEndGenerationAsync(
            Task pendingExecution,
            Exception cleanupFailure)
        {
            Exception pendingFailure = null;
            try
            {
                await (pendingExecution ?? Task.CompletedTask).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                pendingFailure = exception;
            }

            if (cleanupFailure != null && pendingFailure != null)
            {
                throw new AggregateException(cleanupFailure, pendingFailure);
            }

            if (cleanupFailure != null) throw cleanupFailure;
            if (pendingFailure != null) throw pendingFailure;
        }

    }

    internal sealed class RecoveryGenerationOperation
    {
        private readonly object _gate = new object();
        private System.Threading.CancellationTokenSource _cancellation;
        private Task _pending = Task.CompletedTask;
        private int _generation;
        private bool _connectionRecoveryPending;

        public int Generation { get { lock (_gate) return _generation; } }
        public Task PendingExecution { get { lock (_gate) return _pending; } }
        public bool ConnectionRecoveryPending
        {
            get { lock (_gate) return _connectionRecoveryPending; }
            set { lock (_gate) _connectionRecoveryPending = value; }
        }

        public void Begin()
        {
            lock (_gate)
            {
                if (_cancellation != null)
                    throw new InvalidOperationException("Recovery generation is already active.");
                _cancellation = new System.Threading.CancellationTokenSource();
            }
        }

        public bool IsCurrent(int generation)
        {
            lock (_gate) return _cancellation != null && _generation == generation;
        }

        public bool TryReserve(out System.Threading.CancellationToken token,
            out TaskCompletionSource<bool> reservation)
        {
            lock (_gate)
            {
                if (_cancellation == null)
                {
                    token = default;
                    reservation = null;
                    return false;
                }

                token = _cancellation.Token;
                reservation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pending = Task.WhenAll(_pending, reservation.Task);
                return true;
            }
        }

        public Task EndAsync()
        {
            System.Threading.CancellationTokenSource cancellation;
            Task pending;
            lock (_gate)
            {
                _generation++;
                cancellation = _cancellation;
                _cancellation = null;
                pending = _pending;
                _pending = Task.CompletedTask;
                _connectionRecoveryPending = false;
            }

            if (cancellation != null && !cancellation.IsCancellationRequested)
                cancellation.Cancel();
            return CompleteAsync(pending, cancellation);
        }

        private static async Task CompleteAsync(Task pending,
            System.Threading.CancellationTokenSource cancellation)
        {
            try { await pending.ConfigureAwait(false); }
            finally { cancellation?.Dispose(); }
        }
    }
}
