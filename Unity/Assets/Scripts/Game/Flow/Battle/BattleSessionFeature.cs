using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow.Snapshot;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSessionFeature : IGamePhaseFeature
    {
        private const float FixedDelta = 1f / 30f;

        private readonly IBattleBootstrapper _bootstrapper;

        private BattleLogicSession _session;
        private BattleStartPlan _plan;

        private BattleContext _ctx;

        private FrameSnapshotDispatcher _snapshots;
        private BattleSnapshotPipeline _pipeline;
        private BattleCmdHandler _cmdHandler;

        private int _lastFrame;
        private float _tickAcc;

        public BattleSessionFeature(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);

            _plan = _bootstrapper?.Build() ?? default;
            StartSession();

            if (_ctx != null)
            {
                _ctx.Plan = _plan;
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }

            if (_plan.AutoConnect) _session?.Connect();
            if (_plan.AutoCreateWorld) CreateWorld();
            if (_plan.AutoJoin) _session?.Join(new JoinWorldRequest(new WorldId(_plan.WorldId), new PlayerId(_plan.PlayerId)));
            if (_plan.AutoReady)
            {
                var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_plan.PlayerId), opCode: (int)MobaOpCode.Ready, payload: Array.Empty<byte>());
                _session?.SubmitInput(new SubmitInputRequest(new WorldId(_plan.WorldId), cmd));
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            StopSession();

            if (_ctx != null)
            {
                _ctx.Session = null;
            }

            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_session == null) return;

            _tickAcc += deltaTime;
            while (_tickAcc >= FixedDelta)
            {
                _session.Tick(FixedDelta);
                _tickAcc -= FixedDelta;
            }
        }

        private void StartSession()
        {
            StopSession();

            var syncMode = _plan.SyncMode;

            var logicMode = syncMode == BattleSyncMode.SnapshotAuthority
                ? BattleLogicMode.Remote
                : BattleLogicMode.Local;

            var opts = new BattleLogicSessionOptions
            {
                Mode = logicMode,
                WorldId = new WorldId(string.IsNullOrEmpty(_plan.WorldId) ? "room_1" : _plan.WorldId),
                WorldType = string.IsNullOrEmpty(_plan.WorldType) ? "battle" : _plan.WorldType,
                ClientId = string.IsNullOrEmpty(_plan.ClientId) ? "battle_client" : _plan.ClientId,
                PlayerId = string.IsNullOrEmpty(_plan.PlayerId) ? "p1" : _plan.PlayerId,
                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
                ScanAssemblies = new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                NamespacePrefixes = new[] { "AbilityKit" }
            };

            _session = BattleLogicSessionHost.Start(opts);
            _session.FrameReceived += OnFrame;

            _snapshots = new FrameSnapshotDispatcher(_session);
            _pipeline = new BattleSnapshotPipeline(_ctx, _snapshots);
            _cmdHandler = new BattleCmdHandler(_ctx, _snapshots);
            BattleSnapshotRegistry.RegisterAll(_snapshots, _pipeline, _pipeline, _cmdHandler);

            _lastFrame = 0;
            _tickAcc = 0f;

            if (_ctx != null)
            {
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
                _ctx.FrameSnapshots = _snapshots;
                _ctx.SnapshotPipeline = _pipeline;
                _ctx.CmdHandler = _cmdHandler;
            }
        }

        private void StopSession()
        {
            if (_session == null) return;

            try
            {
                _session.FrameReceived -= OnFrame;
                _pipeline?.Dispose();
                _cmdHandler?.Dispose();
                _snapshots?.Dispose();
                BattleLogicSessionHost.Stop();
            }
            catch
            {
            }
            finally
            {
                if (_ctx != null)
                {
                    _ctx.SnapshotPipeline = null;
                    _ctx.CmdHandler = null;
                    _ctx.FrameSnapshots = null;
                }
                _cmdHandler = null;
                _pipeline = null;
                _snapshots = null;
                _session = null;
            }
        }

        private void CreateWorld()
        {
            if (_session == null) return;

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                new[] { "AbilityKit" }
            );

            builder.AddModule(new MobaConfigWorldModule());

            var options = new WorldCreateOptions(new WorldId(_plan.WorldId), _plan.WorldType)
            {
                ServiceBuilder = builder
            };

            var req = new CreateWorldRequest(options, _plan.CreateWorldOpCode, _plan.CreateWorldPayload);
            _session.CreateWorld(req);
        }

        private void OnFrame(FramePacket packet)
        {
            _lastFrame = packet.Frame.Value;

            if (_ctx != null)
            {
                _ctx.LastFrame = _lastFrame;
            }
        }
    }
}
