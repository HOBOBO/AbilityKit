using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow.Snapshot;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Battle.Transport;
using UnityEngine;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.Impl.Moba.Systems;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSessionFeature : IGamePhaseFeature
    {
        private const float FixedDelta = 1f / 30f;
        private const int StateHashRecordIntervalFrames = 10;
        private const int ReplaySeekChunkFrames = 300;
        private const int RollbackSeekProbeFrames = 120;

        private readonly IBattleBootstrapper _bootstrapper;

        private BattleLogicSession _session;
        private BattleStartPlan _plan;

        private BattleContext _ctx;

        private FrameSnapshotDispatcher _snapshots;
        private BattleSnapshotPipeline _pipeline;
        private BattleCmdHandler _cmdHandler;

        private LockstepReplayDriver _replay;

        private int _lastFrame;
        private float _tickAcc;

        private bool _firstFrameReceived;

        public event Action SessionStarted;
        public event Action FirstFrameReceived;

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

            SessionStarted?.Invoke();

            if (_ctx != null)
            {
                _ctx.Plan = _plan;
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }

            ApplyAutoPlanActions();
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            StopSession();

            _firstFrameReceived = false;

            if (_ctx != null)
            {
                _ctx.Session = null;
            }

            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_session == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_replay != null)
            {
                if (Input.GetKeyDown(KeyCode.P))
                {
                    if (_replay.IsPlaying) _replay.Pause();
                    else _replay.Play();
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    _replay.SeekToStart();
                }

                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    var target = Math.Max(0, _lastFrame + ReplaySeekChunkFrames);
                    SeekReplayToFrame(target);
                }

                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    var target = Math.Max(0, _lastFrame - ReplaySeekChunkFrames);
                    SeekReplayToFrame(target);
                }
            }
#endif

            _tickAcc += deltaTime;
            while (_tickAcc >= FixedDelta)
            {
                var nextFrame = _lastFrame + 1;
                _replay?.Pump(_session, nextFrame);
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

            if (_plan.EnableInputReplay)
            {
                opts.EnableRollback = true;
                opts.RollbackHistoryFrames = 1200;
                opts.RollbackCaptureEveryNFrames = 30;
            }

            if (logicMode == BattleLogicMode.Remote)
            {
                IBattleLogicTransport transport;

                if (_plan.UseGatewayTransport)
                {
                    Debug.LogWarning($"Gateway transport is selected but no concrete ITransport adapter is wired yet. Fallback to NullBattleLogicTransport. Host={_plan.GatewayHost}, Port={_plan.GatewayPort}");
                    transport = new NullBattleLogicTransport();
                }
                else
                {
                    transport = new NullBattleLogicTransport();
                }

                _session = BattleLogicSessionHost.Start(opts, remoteTransport: transport);
            }
            else
            {
                _session = BattleLogicSessionHost.Start(opts);
            }
            _session.FrameReceived += OnFrame;

            _snapshots = new FrameSnapshotDispatcher(_session);
            _pipeline = new BattleSnapshotPipeline(_ctx, _snapshots);
            _cmdHandler = new BattleCmdHandler(_ctx, _snapshots);
            BattleSnapshotRegistry.RegisterAll(_snapshots, _pipeline, _pipeline, _cmdHandler);

            _lastFrame = 0;
            _tickAcc = 0f;
            _firstFrameReceived = false;

            if (_plan.EnableInputReplay)
            {
                var file = LockstepJsonInputRecordReader.Load(_plan.InputReplayPath);
                _replay = new LockstepReplayDriver(new WorldId(_plan.WorldId), file);
            }

            if (_ctx != null)
            {
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
                _ctx.FrameSnapshots = _snapshots;
                _ctx.SnapshotPipeline = _pipeline;
                _ctx.CmdHandler = _cmdHandler;

                if (_plan.EnableInputRecording)
                {
                    _ctx.InputRecordWriter?.Dispose();
                    _ctx.InputRecordWriter = new LockstepJsonInputRecordWriter(
                        _plan.InputRecordOutputPath,
                        new LockstepInputRecordMeta
                        {
                            WorldId = _plan.WorldId,
                            WorldType = _plan.WorldType,
                            TickRate = 30,
                            RandomSeed = 0,
                            PlayerId = _plan.PlayerId,
                            StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        });
                }
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

                _replay = null;
                _cmdHandler = null;
                _pipeline = null;
                _snapshots = null;
                _session = null;
            }
        }

        private void ApplyAutoPlanActions()
        {
            if (_plan.AutoConnect) _session?.Connect();
            if (_plan.AutoCreateWorld) CreateWorld();
            if (_plan.AutoJoin) _session?.Join(new JoinWorldRequest(new WorldId(_plan.WorldId), new PlayerId(_plan.PlayerId)));
            if (_plan.AutoReady)
            {
                var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_plan.PlayerId), opCode: (int)MobaOpCode.Ready, payload: Array.Empty<byte>());
                _session?.SubmitInput(new SubmitInputRequest(new WorldId(_plan.WorldId), cmd));
            }
        }

        private void SeekReplayToFrame(int targetFrame)
        {
            if (!_plan.EnableInputReplay) return;
            if (targetFrame < 0) targetFrame = 0;

            // Fast path: seek forward by fast-forwarding within the same session.
            if (_session != null && _replay != null && targetFrame > _lastFrame)
            {
                _tickAcc = 0f;

                for (int f = _lastFrame + 1; f <= targetFrame; f++)
                {
                    _replay.Pump(_session, f);
                    _session.Tick(FixedDelta);
                }

                _lastFrame = targetFrame;
                if (_ctx != null) _ctx.LastFrame = _lastFrame;
                return;
            }

            // Fast path: seek backward within rollback history without restarting.
            if (_session != null && _session.RollbackModule != null && targetFrame <= _lastFrame)
            {
                var worldId = new WorldId(_plan.WorldId);
                var probeStart = Math.Max(0, targetFrame - RollbackSeekProbeFrames);
                for (int f = targetFrame; f >= probeStart; f--)
                {
                    if (_session.RollbackModule.TryRollbackAndReplay(worldId, new FrameIndex(f), new FrameIndex(targetFrame), FixedDelta))
                    {
                        _lastFrame = targetFrame;
                        if (_ctx != null) _ctx.LastFrame = _lastFrame;
                        return;
                    }
                }
            }

            StopSession();
            StartSession();
            ApplyAutoPlanActions();

            if (_replay == null) return;
            _replay.SeekToStart();

            _tickAcc = 0f;

            for (int f = 1; f <= targetFrame; f++)
            {
                _replay.Pump(_session, f);
                _session.Tick(FixedDelta);
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
                ServiceBuilder = builder,
                EntitasContextsFactory = new MobaEntitasContextsFactory()
            };

            options.Modules.Add(new MobaWorldBootstrapModule());

            var req = new CreateWorldRequest(options, _plan.CreateWorldOpCode, _plan.CreateWorldPayload);
            _session.CreateWorld(req);
        }

        private void OnFrame(FramePacket packet)
        {
            _lastFrame = packet.Frame.Value;

            if (!_firstFrameReceived)
            {
                _firstFrameReceived = true;
                FirstFrameReceived?.Invoke();
            }

            if (_ctx != null)
            {
                _ctx.LastFrame = _lastFrame;

                if (_replay != null)
                {
                    if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent hs) && hs != null)
                    {
                        if (!_replay.TryValidateStateHashOnce(hs.Frame, hs.Version, hs.Hash, out var expected))
                        {
                            Debug.LogError($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
                            _replay.Pause();
                        }
                    }
                }

                if (_plan.EnableInputRecording && _ctx.InputRecordWriter != null)
                {
                    if (packet.Snapshot.HasValue)
                    {
                        var s = packet.Snapshot.Value;
                        _ctx.InputRecordWriter.AppendSnapshot(_lastFrame, s.OpCode, s.Payload);
                    }

                    var interval = StateHashRecordIntervalFrames;
                    if (interval <= 0) interval = 10;

                    if ((_lastFrame % interval) == 0)
                    {
                        if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent h) && h != null)
                        {
                            _ctx.InputRecordWriter.AppendStateHash(h.Frame, h.Version, h.Hash);
                        }
                    }
                }
            }
        }
    }
}
