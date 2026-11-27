using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Impl.Pipeline.Skill;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Pipeline;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using SkillReplayCase = AbilityKit.BattleScenario.Editor.LocalStateSyncReplay.SkillReplayCase;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    /// <summary>
    /// 用内置时间表给正式客户端投递类型化快照。模拟的是连接和权威来源，
    /// 正式配方通过快照提交端口导入权威世界，再由战斗候选世界重演。
    /// </summary>
    internal static partial class MobaStateSyncClientReplay
    {
        private const string BattleId = "scenario-statesync-battle";
        private const int ModelId = 3005;
        private const int ActorId = 7;
        private const int ActorOnlySnapshotSchema = 1;
        private const int TickRate = 30;
        private static readonly WorldId World = new WorldId("1");
        private static readonly PlayerId Player = new PlayerId("1");

        internal static LocalStateSyncReplay CaptureSkill(SkillReplayCase recipe) => Capture(recipe);

        private static NetworkTransport CreateLocalTransport()
        {
            var options = NetworkTransportOptionsFactory.Create(new NetworkTransportOptionsFactoryOptions
            {
                Host = "offline",
                Port = 1,
                RoomId = 1,
                BattleId = BattleId,
                SessionToken = "local",
                TransportFactory = () => throw new NotSupportedException("Offline preview does not create a transport."),
                UseFixedBaselineStateSync = true,
                UseScopedStateSyncInput = true,
                ScopedInputWorldId = 1,
                ScopedInputPlayerId = 1,
            });
            options.ConnectionFactory = () => new DisconnectedConnection();
            options.TransportFactory = null;
            return new NetworkTransport(options);
        }

        private static LocalStateSyncReplay Capture(SkillReplayCase skillRecipe)
        {
            var rejectBeforeRelease = skillRecipe == SkillReplayCase.BattleCastMoveRejectBeforeRelease;
            var windup = skillRecipe is SkillReplayCase.BattleCastMoveWindup or SkillReplayCase.BattleCastWindup;
            var combinedBattleCandidate = rejectBeforeRelease || skillRecipe == SkillReplayCase.BattleCastMoveWindup;
            var formalFlight = skillRecipe is SkillReplayCase.BattleFlightRestore or SkillReplayCase.BattleFlightReplay or SkillReplayCase.BattleFlightReject;
            var formalCast = windup || rejectBeforeRelease || skillRecipe is SkillReplayCase.BattleCastConfirmed or SkillReplayCase.BattleCastRejected;
            var tickRate = formalCast || formalFlight ? BattleCastTickRate : HasEconomy(skillRecipe) ? 20 : TickRate;
            var fixedDelta = 1f / tickRate;
            using var transport = CreateLocalTransport();
            using var replacement = skillRecipe == SkillReplayCase.GenerationChanged ? CreateLocalTransport() : null;
            using var session = new BattleLogicSession(new BattleLogicSessionOptions
            {
                Mode = BattleLogicMode.Remote,
                WorldId = World,
                PlayerId = Player.Value,
                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            }, transport);
            using var replication = new BattleReplicationRuntime();
            var context = BattleContext.Rent();
            var handles = new BattleSessionHandles { Session = session };
            var simulation = new BattleSimulationRuntime(new BattleSessionState(), handles, new LocalAuthorityInstaller());
            using var recovery = new AuthoritativeStateRecoveryRuntime(replication,
                new ReliableBattleEventDeliveryRuntime(_ => System.Threading.Tasks.Task.CompletedTask),
                new BattleAuthoritativeWorldRecoveryPort(simulation, handles));
            var world = new BattleStartPlanWorldOptions(World.Value, "battle", "scenario", Player.Value, tickRate, 0);
            var sync = new BattleStartPlanSyncOptions(BattleSyncMode.SnapshotAuthority, default, null);
            var authority = new BattleStartPlanAuthorityOptions(false, false);
            var gateway = new BattleStartPlanGatewayOptions(true, "offline", 1, 1, "local", "", "", false, false,
                "", 0, 0, BattleId, useScopedStateSyncInput: true);
            var planOptions = new BattleStartPlanOptions(in world, BattleHostMode.GatewayRemote, in sync, in authority,
                in gateway, default, default, default, default);
            var plan = new BattleStartPlan(in planOptions);
            var stream = Guid.NewGuid();
            var inputGeneration = transport.Options.StateSyncInputGeneration;
            var revision = 0L;
            try
            {
                recovery.AttachStateSyncTransport(transport, 1, null, context, in plan, fixedDelta,
                    _ => { }, () => { });
                if (formalFlight)
                    return CaptureBattleFlightTrack(skillRecipe, stream, inputGeneration, recovery,
                        () => simulation.RemoteDrivenLastTickedFrame,
                        actorId => recovery.ResolveCommittedProjection().ExtractFull(actorId));
                if (formalCast)
                    return CaptureBattleCastTrack(skillRecipe == SkillReplayCase.BattleCastConfirmed, windup, combinedBattleCandidate,
                        rejectBeforeRelease, stream, inputGeneration,
                        recovery, () => simulation.RemoteDrivenLastTickedFrame, actorId =>
                            ToPose(recovery.ResolveCommittedProjection().ExtractFull(actorId)));
                if (HasEconomy(skillRecipe))
                {
                    var scope = new LocalStateSyncCastScope(recovery.LocalReplayGeneration, 1, stream, inputGeneration,
                        1, ActorId, 1);
                    return CaptureEconomyTrack(skillRecipe == SkillReplayCase.EconomyConfirmed, in scope, tickRate, recovery,
                        tick => CreateSnapshot(tick, 0f, 0f, null), () => simulation.RemoteDrivenLastTickedFrame);
                }
                return CaptureSkillTrack(skillRecipe, tickRate, recovery, CreateSnapshot,
                    () => simulation.RemoteDrivenLastTickedFrame, ReplaceGeneration);
            }
            finally
            {
                try { recovery.Dispose(); }
                finally
                {
                    try { simulation.DisposeRemoteDrivenWorld(); }
                    finally { BattleContext.Return(context); }
                }
            }

            void ReplaceGeneration()
            {
                if (replacement == null)
                    throw new InvalidOperationException("当前配方不允许切换会话代际。");
                recovery.AttachStateSyncTransport(replacement, 1, null, context, in plan, fixedDelta,
                    _ => { }, () => { });
                inputGeneration = replacement.Options.StateSyncInputGeneration;
                stream = Guid.NewGuid();
                revision = 0;
            }

            GatewayStateSyncSnapshot CreateSnapshot(int tick, float z, float moveZ, long? terminal)
            {
                var baseline = revision == 0 ? 0L : 1L;
                revision++;
                GatewayStateSyncMovementProof? proof = terminal is long prefix
                    ? new GatewayStateSyncMovementProof(inputGeneration, 1, 1, stream, revision, baseline, tick,
                        prefix, ActorId, 1, 0f, 0f, z, 0f, 0f, 0f, 1f, 0f, moveZ, 30f, true, true)
                    : null;
                var actor = new GatewayStateSyncActorSnapshot(ActorId, 0f, 0f, z, 0f, 0f, 0f, 100f, 100f,
                    1, kind: (int)SpawnEntityKind.Character, code: ModelId, ownerNetId: 1);
                return new GatewayStateSyncSnapshot(1, tick, 0d, baseline == 0,
                    new[] { actor }, ActorOnlySnapshotSchema, eventEpoch: "scenario-epoch",
                    usesFixedBaseline: true, stateStreamGeneration: stream, snapshotId: revision, baseSnapshotId: baseline,
                    movementProof: proof);
            }

        }

        private static LocalStateSyncReplay.ActorPose ToPose(ActorProjectionData actor) =>
            new LocalStateSyncReplay.ActorPose
            {
                X = actor.PosX, Y = actor.PosY, Z = actor.PosZ,
                Qx = actor.RotX, Qy = actor.RotY, Qz = actor.RotZ, Qw = actor.RotW,
            };

        private static LocalStateSyncReplay.ActorPose ToPose(Transform3 pose) =>
            new LocalStateSyncReplay.ActorPose
            {
                X = pose.Position.X, Y = pose.Position.Y, Z = pose.Position.Z,
                Qx = pose.Rotation.X, Qy = pose.Rotation.Y, Qz = pose.Rotation.Z, Qw = pose.Rotation.W,
            };

        private static bool HasEconomy(SkillReplayCase? recipe) =>
            recipe is SkillReplayCase.EconomyConfirmed or SkillReplayCase.EconomyRejected;

        private static LocalStateSyncReplay CaptureSkillTrack(SkillReplayCase recipe, int tickRate, AuthoritativeStateRecoveryRuntime recovery,
            Func<int, float, float, long?, GatewayStateSyncSnapshot> createSnapshot, Func<int> readAppliedTick,
            Action replaceGeneration)
        {
            const int baselineTick = 10, firstInputTick = 11, anchorTick = 12, pendingTick = 14, deliveryTick = 15;
            const long terminalSequence = 2, pendingSequence = 3;
            var accepted = recipe != SkillReplayCase.Rejected;
            var fixedDelta = 1f / tickRate;
            var oldGeneration = recovery.LocalReplayGeneration;
            var initial = createSnapshot(baselineTick, 0f, 0f, terminalSequence - 1);
            if (!recovery.TryCommitLocalReplaySnapshot(oldGeneration, in initial, out var initialReceipt) ||
                initialReceipt is not { } initialProof || readAppliedTick() != baselineTick)
                throw new InvalidOperationException("施法样例缺少已提交的全量基线。");

            using var authority = new CastTrack(false, fixedDelta);
            using var prediction = new CastTrack(false, fixedDelta);
            var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
            var steps = new List<LocalStateSyncReplay.SkillReplayStep>();
            var history = new Dictionary<int, long>();
            var pending = new HashSet<long>();
            CastAnchor anchor = default;
            GatewayStateSyncSnapshot delivery = default;
            LocalStateSyncReplay.SkillFallbackObservation fallback = null;
            long? replayedSequence = null;
            long consumedThrough = 0;

            for (var tick = baselineTick; tick <= deliveryTick; tick++)
            {
                authority.Advance(tick, accepted && tick == anchorTick);
                var sequence = tick == firstInputTick ? terminalSequence : tick == pendingTick ? pendingSequence : 0;
                history.Add(tick, sequence);
                if (sequence != 0) pending.Add(sequence);
                prediction.Advance(tick, sequence != 0);
                var oldPrediction = prediction.Observation;
                var published = oldPrediction;
                if (tick == anchorTick)
                {
                    anchor = authority.Capture();
                    delivery = createSnapshot(tick, 0f, 0f, recipe == SkillReplayCase.MissingProof ? (long?)null : terminalSequence);
                }
                if (tick == deliveryTick)
                {
                    if (recipe == SkillReplayCase.GenerationChanged)
                    {
                        replaceGeneration();
                        prediction.Clear();
                        authority.Clear();
                        pending.Clear();
                        history.Clear();
                        anchor = default;
                        var nextGeneration = recovery.LocalReplayGeneration;
                        var next = createSnapshot(deliveryTick, 0f, 0f, terminalSequence - 1);
                        if (nextGeneration == oldGeneration ||
                            !recovery.TryCommitLocalReplaySnapshot(nextGeneration, in next, out var nextReceipt) ||
                            nextReceipt is not { } nextProof || nextProof.InputGeneration == initialProof.InputGeneration ||
                            nextProof.StreamGeneration == initialProof.StreamGeneration ||
                            recovery.TryCommitLocalReplaySnapshot(oldGeneration, in delivery, out var staleReceipt) ||
                            staleReceipt.HasValue || readAppliedTick() != deliveryTick ||
                            recovery.CommittedMovementProof is not { } currentProof || !currentProof.Matches(in next))
                            throw new InvalidOperationException("换代后未保留新权威基线，或接受了旧会话投递。");
                        fallback = ObserveFallback(SkillReplayCase.GenerationChanged);
                        published = prediction.Observation;
                    }
                    else
                    {
                        if (!recovery.TryCommitLocalReplaySnapshot(oldGeneration, in delivery, out var receipt) ||
                            readAppliedTick() != anchorTick)
                            throw new InvalidOperationException("施法样例未采用迟到权威截面。");
                        if (receipt is not { } proof)
                            fallback = ObserveFallback(SkillReplayCase.MissingProof);
                        else
                        {
                            if (!proof.Matches(in delivery) || proof.Tick != anchorTick ||
                                proof.InputGeneration != initialProof.InputGeneration || proof.StreamGeneration != initialProof.StreamGeneration ||
                                proof.PlayerId != initialProof.PlayerId || proof.ActorId != initialProof.ActorId ||
                                proof.EntityVersion != initialProof.EntityVersion || proof.TerminalThrough != terminalSequence)
                                throw new InvalidOperationException("施法截面与本次提交的输入证明不一致。");
                            consumedThrough = proof.TerminalThrough;
                            pending.RemoveWhere(input => input <= consumedThrough);
                            if (recipe == SkillReplayCase.HistoryGap) history.Remove(anchorTick + 1);
                            for (var replayTick = anchorTick + 1; replayTick <= deliveryTick; replayTick++)
                            {
                                if (history.ContainsKey(replayTick)) continue;
                                fallback = ObserveFallback(SkillReplayCase.HistoryGap, replayTick);
                                break;
                            }
                        }

                        if (fallback != null)
                        {
                            if (fallback.Reason != recipe)
                                throw new InvalidOperationException("施法配方触发了意外的降级路径。");
                            prediction.Clear();
                            prediction.Restore(anchor);
                            published = prediction.Observation;
                        }
                        else
                        {
                            if (checkpoints[anchorTick - baselineTick].Skill.Authority.Matches(
                                    checkpoints[anchorTick - baselineTick].Skill.OldPrediction))
                                throw new InvalidOperationException("样例必须触发同 Tick 不匹配。");
                            using var candidate = new CastTrack(false, fixedDelta);
                            candidate.Restore(anchor);
                            for (var replayTick = anchorTick + 1; replayTick <= deliveryTick; replayTick++)
                            {
                                var input = history[replayTick];
                                var shouldReplay = pending.Contains(input);
                                candidate.Advance(replayTick, shouldReplay);
                                if (shouldReplay) replayedSequence = input;
                                steps.Add(new LocalStateSyncReplay.SkillReplayStep { Tick = replayTick, State = candidate.Observation });
                            }
                            if (candidate.Starts != (accepted ? 0 : 1))
                                throw new InvalidOperationException("重演重新启动了已终结施法，或遗漏了应执行的待确认输入。");
                            published = candidate.Observation;
                        }

                        if (recovery.TryCommitLocalReplaySnapshot(oldGeneration, in delivery, out var duplicateProof) ||
                            duplicateProof.HasValue)
                            throw new InvalidOperationException("重复交付产生了第二次提交回执。");
                    }
                }
                checkpoints.Add(new LocalStateSyncReplay.Checkpoint
                {
                    Command = tick == deliveryTick ? "迟到施法截面" : "本地施法推进",
                    DeliveryTick = tick,
                    BaselineTick = fallback?.Reason == SkillReplayCase.GenerationChanged ? deliveryTick : baselineTick,
                    Generation = recovery.LocalReplayGeneration == oldGeneration ? 1 : 2,
                    AppliedTick = readAppliedTick(),
                    Skill = new LocalStateSyncReplay.SkillTickObservation
                    {
                        Authority = authority.Observation, OldPrediction = oldPrediction, Published = published,
                    },
                });
            }

            var replay = new LocalStateSyncReplay
            {
                CaseId = "moba-local-cast-window",
                Variant = recipe switch
                {
                    SkillReplayCase.Confirmed => "confirmed",
                    SkillReplayCase.Rejected => "rejected",
                    SkillReplayCase.HistoryGap => "history-gap",
                    SkillReplayCase.MissingProof => "without-proof",
                    SkillReplayCase.GenerationChanged => "generation-changed",
                    _ => throw new ArgumentOutOfRangeException(nameof(recipe)),
                },
                Version = LocalStateSyncReplay.SkillTrackVersion, TickRate = tickRate,
                SkillDelivery = new LocalStateSyncReplay.SkillDeliveryObservation
                {
                    PrimaryLabel = "施法前摇", SecondaryLabel = "再次按下", FirstInputTick = firstInputTick,
                    AnchorTick = anchorTick, InputTick = pendingTick, DeliveryTick = deliveryTick,
                    TerminalThroughSequence = consumedThrough, PendingSequence = pendingSequence,
                    ReplayedSequence = replayedSequence, Steps = steps.ToArray(), CastProgressOnly = true, Fallback = fallback,
                },
                Checkpoints = checkpoints.ToArray(),
            };
            replay.Validate();
            return replay;

            LocalStateSyncReplay.SkillFallbackObservation ObserveFallback(SkillReplayCase reason, int? missingTick = null) =>
                new LocalStateSyncReplay.SkillFallbackObservation
                {
                    Reason = reason, RetainedInputCount = pending.Count, MissingHistoryTick = missingTick,
                };
        }

        /// <summary>
        /// 运行本地施法配方，可附带既有技能经济与窗口服务，但不执行战斗结算。
        /// 捕获类型化状态供同一离线配方恢复，不是网络协议或帧同步检查点。
        /// </summary>
        private sealed class CastTrack : ILocalStateSyncCastWorld<CastAnchor, LocalStateSyncReplay.SkillState>
        {
            private readonly SkillCastPipeline _pipeline = new SkillCastPipeline();
            private readonly SkillPipelineConfig _config = new SkillPipelineConfig(1, "local-cast-windup");
            private readonly CastEconomyScope _economy;
            private readonly float _fixedDelta;
            private IAbilityPipelineRun<SkillPipelineContext> _run;
            private SkillPipelineContext _context;

            internal CastTrack(bool economy, float fixedDelta)
            {
                _fixedDelta = fixedDelta;
                if (economy) _economy = new CastEconomyScope(_pipeline);
                else _pipeline.AddPhase(new AbilityDelayPhase<SkillPipelineContext>(1f));
            }

            internal int Starts { get; private set; }
            internal LocalStateSyncReplay.SkillState Observation => _economy?.Observe(_context) ?? new LocalStateSyncReplay.SkillState
            {
                CastElapsedMs = _run?.Phases[0] is AbilityDelayPhase<SkillPipelineContext> delay
                    ? (int)Math.Round(delay.ElapsedTime * 1000f) : (int?)null,
            };

            internal string Advance(int tick, bool press)
            {
                _economy?.Align(tick, _fixedDelta);
                var rejection = press ? _economy?.ReadInputRejection() : null;
                if (press && rejection == null && _run == null)
                {
                    _context = _economy?.CreateContext() ?? new SkillPipelineContext();
                    _run = _pipeline.Start(_config, _context);
                    Starts++;
                    _run.Tick(0f);
                }
                _run?.Tick(_fixedDelta);
                return rejection;
            }

            internal CastAnchor Capture() => new CastAnchor(
                _run == null ? null : SkillPipelineRollback.Capture(_run, _config), _economy?.Capture(_context));

            internal void Restore(CastAnchor anchor)
            {
                _context = _economy?.Restore(anchor.Economy);
                if (anchor.Progress != null)
                    _run = SkillPipelineRollback.Restore(_pipeline, _config, _context ?? new SkillPipelineContext(), anchor.Progress);
            }

            void ILocalStateSyncCastWorld<CastAnchor, LocalStateSyncReplay.SkillState>.Restore(CastAnchor anchor) => Restore(anchor);

            bool ILocalStateSyncCastWorld<CastAnchor, LocalStateSyncReplay.SkillState>.TryStep(int tick, SkillInputEvent? input, out string rejection)
            {
                rejection = Advance(tick, input.HasValue);
                return true;
            }

            LocalStateSyncReplay.SkillState ILocalStateSyncCastWorld<CastAnchor, LocalStateSyncReplay.SkillState>.Read() => Observation;

            internal void Clear()
            {
                SkillPipelineRollback.Detach(_run);
                _run = null;
                _context = null;
            }

            public void Dispose()
            {
                try { Clear(); }
                finally { _economy?.Dispose(); }
            }
        }

        /// <summary>
        /// 前摇进度与可选经济截面在同一配方 Tick 捕获。
        /// </summary>
        private readonly struct CastAnchor
        {
            internal readonly PipelineRunRollbackState Progress;
            internal readonly EconomyAnchor Economy;

            internal CastAnchor(PipelineRunRollbackState progress, EconomyAnchor economy)
            {
                Progress = progress;
                Economy = economy;
            }
        }

        private sealed class LocalAuthorityInstaller : IBattleSessionWorldInstaller
        {
            public void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options)
            {
                if (options.Handles.World != null) return;
                var worlds = new WorldManager(new RegistryWorldFactory(new WorldTypeRegistry().RegisterEntitasWorld("battle")));
                var runtimeOptions = new HostRuntimeOptions();
                var runtime = new HostRuntime(worlds, runtimeOptions);
                StateSyncWorldRuntime.CreateModules(options.FixedDeltaSeconds).InstallAll(runtime, runtimeOptions);

                var services = new WorldContainerBuilder();
                services.AddModule(new DefaultWorldServicesModule());
                services.RegisterType<MobaActorRegistry, MobaActorRegistry>(WorldLifetime.Singleton);
                services.RegisterType<MobaEntityManager, MobaEntityManager>(WorldLifetime.Singleton);
                services.RegisterType<IMobaActorSpawnService, MobaActorSpawnService>(WorldLifetime.Singleton);
                services.RegisterType<IActorProjectionProducer, MobaActorProjectionProducer>(WorldLifetime.Singleton);
                services.Register<MobaLogicWorldStateImporter>(WorldLifetime.Singleton, resolver =>
                    new MobaLogicWorldStateImporter(resolver.Resolve<MobaActorRegistry>(), resolver.Resolve<IMobaActorSpawnService>(), null));
                var create = new WorldCreateOptions(World, "battle") { ServiceBuilder = services };
                create.SetEntitasContextsFactory(new MobaEntitasContextsFactory());
                options.Handles.BindWorldRuntime(new RemoteDrivenWorldRuntime(World, worlds, runtime, runtime.CreateWorld(create)));
                options.ResetTickState();
            }

            public void EnsureConfirmedAuthorityStarted(ConfirmedAuthorityWorldInstallOptions options) =>
                throw new NotSupportedException("此用例不启动帧同步权威世界。");
        }

        private sealed class DisconnectedConnection : IConnection
        {
            public ConnectionState State => ConnectionState.Disconnected;
            public bool IsConnected => false;
            public event Action Connected { add { } remove { } }
            public event Action Disconnected { add { } remove { } }
            public event Action<Exception> Error { add { } remove { } }
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived { add { } remove { } }
            public event Action<uint, ArraySegment<byte>> ServerPushReceived { add { } remove { } }
            public event Action<string, string> Kicked { add { } remove { } }
            public void Open(string host, int port) => throw new NotSupportedException("此用例禁止连接。");
            public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0) =>
                throw new NotSupportedException("此用例禁止发送。");
            public void Close() { }
            public void Tick(float deltaTime) { }
            public void Dispose() { }
        }
    }
}
