using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
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
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    /// <summary>
    /// 用内置时间表给正式客户端投递类型化快照。模拟的是连接和权威来源，
    /// 快照提交、逻辑世界导入、预测和逐 Tick 重演均走正式实现。
    /// </summary>
    internal static class MobaStateSyncClientReplay
    {
        private const string BattleId = "scenario-statesync-movement";
        private const int ModelId = 3005;
        private const int ActorId = 7;
        private const int ActorOnlySnapshotSchema = 1;
        private const int TickRate = 30;
        private const float FixedDelta = 1f / TickRate;
        private static readonly WorldId World = new WorldId("1");
        private static readonly PlayerId Player = new PlayerId("1");

        internal static LocalStateSyncReplay Capture()
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
            using var transport = new NetworkTransport(options);
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
            var world = new BattleStartPlanWorldOptions(World.Value, "battle", "scenario", Player.Value, TickRate, 0);
            var sync = new BattleStartPlanSyncOptions(BattleSyncMode.SnapshotAuthority, default, null);
            var authority = new BattleStartPlanAuthorityOptions(false, false, true);
            var gateway = new BattleStartPlanGatewayOptions(true, "offline", 1, 1, "local", "", "", false, false,
                "", 0, 0, BattleId, useScopedStateSyncInput: true);
            var planOptions = new BattleStartPlanOptions(in world, BattleHostMode.GatewayRemote, in sync, in authority,
                in gateway, default, default, default, default);
            var plan = new BattleStartPlan(in planOptions);
            var stream = Guid.NewGuid();
            var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
            var movement = default(StateSyncMovementPrediction);
            var sequence = 0L;
            var actualTerminal = 0L;
            var revision = 0L;
            var publishedTick = 0;

            try
            {
                recovery.AttachStateSyncTransport(transport, 1, null, context, in plan, FixedDelta,
                    _ => { }, () => { }, session);
                movement = recovery.MovementPrediction ?? throw new InvalidOperationException("未启用状态同步移动预测。");

                Submit(10, 0f, 0f);
                Deliver(10, 0f, 0f, 1);
                Record(10, "权威全量基线", delivered: true);
                Submit(11, 0f, 1f);
                Advance(11, "预测移动 · 原始同 Tick 样本");
                Advance(12, "继续预测 · 空 Tick");
                Submit(13, 0f, 1f);
                Advance(13, "继续预测 · 待终结移动输入");
                Advance(14, "权威到达前 · 当前预测位置");

                Deliver(11, -2f, 1f, 2);
                if (movement.LastSameTickMatch != false || movement.LastReplayTicks != 3)
                    throw new InvalidOperationException("正式客户端未复现预期的同 Tick 偏差或未完整追平。");
                Record(14, "迟到权威 · 回退并追至 Tick 14", delivered: true, comparisonTick: 11);
                Advance(15, "纠偏之后 · 下一 Tick 继续预测");

                var replay = new LocalStateSyncReplay
                {
                    CaseId = BattleId,
                    Variant = "正式客户端 · 类型化本地投递",
                    TickRate = TickRate,
                    ActorModelId = ModelId,
                    PositionLabel = "Z 轴 · 千分之一世界单位",
                    Checkpoints = checkpoints.ToArray(),
                };
                replay.Validate();
                return replay;
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

            void Submit(int tick, float x, float z)
            {
                var command = new PlayerInputCommand(new FrameIndex(tick), Player, MobaOpCodes.Input.Move, null);
                session.SubmitInput(new SubmitInputRequest(World, command), (x, z));
                sequence++;
            }

            void Deliver(int tick, float z, float moveZ, long terminal)
            {
                var baseline = revision == 0 ? 0L : 1L;
                revision++;
                var proof = new GatewayStateSyncMovementProof(options.StateSyncInputGeneration, 1, 1, stream,
                    revision, baseline, tick, terminal, ActorId, 1, 0f, 0f, z, 0f, 0f, 0f, 1f,
                    0f, moveZ, 30f, true, true);
                var actor = new GatewayStateSyncActorSnapshot(ActorId, 0f, 0f, z, 0f, 0f, 0f, 100f, 100f,
                    1, kind: (int)SpawnEntityKind.Character, code: ModelId, ownerNetId: 1);
                recovery.HandleSnapshot(new GatewayStateSyncSnapshot(1, tick, 0d, baseline == 0,
                    new[] { actor }, ActorOnlySnapshotSchema, eventEpoch: "scenario-epoch",
                    usesFixedBaseline: true, stateStreamGeneration: stream, snapshotId: revision, baseSnapshotId: baseline,
                    movementProof: proof));
                if (recovery.CommittedMovementProof is not { } committed || committed.SnapshotId != revision)
                    throw new InvalidOperationException($"快照 {revision} 未通过正式提交路径。");
                actualTerminal = committed.TerminalThrough;
                publishedTick = simulation.RemoteDrivenLastTickedFrame;
            }

            void Advance(int tick, string label)
            {
                movement.Tick(FixedDelta);
                if (!movement.TryGetState(out _, out var actual, out var predicted) || !predicted || actual != tick)
                    throw new InvalidOperationException($"本地预测未推进到 Tick {tick}。");
                Record(tick, label);
            }

            void Record(int tick, string label, bool delivered = false, int? comparisonTick = null)
            {
                var authorityActor = handles.RemoteDriven.Capabilities.ProjectionProducer.ExtractFull(ActorId);
                if (authorityActor.ActorId != ActorId || simulation.RemoteDrivenLastTickedFrame != publishedTick)
                    throw new InvalidOperationException("独立预测错误地改变了权威投影或时钟。");
                var authorityPose = ToPose(authorityActor);
                LocalStateSyncReplay.ActorPose predictionPose = null;
                if (movement.TryGetState(out _, out var predictionTick, out var predicted) && predicted)
                {
                    if (predictionTick != tick || !movement.TryGetProjection(out var producer, out var projectionTick) ||
                        projectionTick != tick)
                        throw new InvalidOperationException("预测投影 Tick 与实际逻辑 Tick 不一致。");
                    predictionPose = ToPose(producer.ExtractFull(ActorId));
                }

                var steps = delivered ? movement.CaptureLastReplay().Select(step => new LocalStateSyncReplay.ReplayStep
                {
                    Tick = step.Tick,
                    Before = Position(step.Before.Position.Z),
                    After = Position(step.After.Position.Z),
                    AfterPose = ToPose(step.After),
                }).ToArray() : Array.Empty<LocalStateSyncReplay.ReplayStep>();
                checkpoints.Add(new LocalStateSyncReplay.Checkpoint
                {
                    Command = label,
                    DeliveryTick = tick,
                    AppliedTick = publishedTick,
                    BaselineTick = 10,
                    Generation = 1,
                    Sequence = sequence,
                    Admission = delivered ? "Accepted" : null,
                    Reconciliation = delivered ? (steps.Length == 0 ? "AuthoritativeOnly" : "Predicted") : null,
                    PublishedRevision = revision,
                    ReconciledRevision = revision,
                    TerminalSequence = actualTerminal,
                    StreamWaiting = recovery.PendingStateImport,
                    Imports = 1,
                    PublishedPosition = Position(authorityPose.Z),
                    Authority = Position(authorityPose.Z),
                    AuthorityPose = authorityPose,
                    Prediction = predictionPose == null ? null : Position(predictionPose.Z),
                    PredictionPose = predictionPose,
                    ComparisonTick = comparisonTick,
                    Pending = Enumerable.Range((int)actualTerminal + 1, (int)(sequence - actualTerminal))
                        .Select(value => (long)value).ToArray(),
                    ReplayTicks = steps.Select(step => step.Tick).ToArray(),
                    Steps = steps,
                });
            }
        }

        private static int Position(float z) => (int)Math.Round(z * 1000f);

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
