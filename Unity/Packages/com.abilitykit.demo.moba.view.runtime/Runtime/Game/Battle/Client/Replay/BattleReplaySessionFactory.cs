using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    internal interface IBattleReplaySessionRuntime : IDisposable
    {
        bool IsActive { get; }
        bool IsPlaying { get; }
        int LastFrame { get; }
        float PlaybackSpeed { get; set; }

        void Play();
        void Pause();
        void PumpAndTick(int frame, float fixedDeltaSeconds);
    }

    internal interface IBattleReplaySessionFactory
    {
        FrameRecordFile Load(string path);
        IBattleReplaySessionRuntime Start(BattleStartPlan plan, FrameRecordFile file);
    }

    internal sealed class DefaultBattleReplaySessionFactory : IBattleReplaySessionFactory
    {
        private readonly IBattleLogicSessionRegistry _registry;

        public DefaultBattleReplaySessionFactory(IBattleLogicSessionRegistry registry = null)
        {
            _registry = registry ?? new DefaultBattleLogicSessionRegistry(publishDebugFacade: false);
        }

        public FrameRecordFile Load(string path)
        {
            return FrameRecordCodecs.Current.Load(path);
        }

        public IBattleReplaySessionRuntime Start(BattleStartPlan plan, FrameRecordFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            try
            {
                var session = _registry.Start(BuildSessionOptions(plan));
                session.Connect();

                BattleSessionFeature.TrySetupProtocolWireSerializerInstaller();
                var world = plan.World;
                var create = plan.CreateWorld;
                var options = SessionMobaWorldBootstrapFactory.CreateWorldOptions(
                    plan,
                    new WorldId(world.WorldId),
                    registerWorldInitData: false);
                session.CreateWorld(new CreateWorldRequest(options, create.OpCode, create.Payload));
                session.Join(new JoinWorldRequest(new WorldId(world.WorldId), new PlayerId(world.PlayerId)));

                return new DefaultBattleReplaySessionRuntime(
                    _registry,
                    session,
                    new FrameReplayDriver(new WorldId(world.WorldId), file));
            }
            catch (Exception startFailure)
            {
                try
                {
                    _registry.Stop();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "Replay session bootstrap and cleanup both failed.",
                        startFailure,
                        cleanupFailure);
                }

                throw;
            }
        }

        private static BattleLogicSessionOptions BuildSessionOptions(BattleStartPlan plan)
        {
            var world = plan.World;
            return new BattleLogicSessionOptions
            {
                Mode = BattleLogicMode.Local,
                WorldId = new WorldId(world.WorldId),
                WorldType = world.WorldType,
                ClientId = $"{world.ClientId}.replay",
                PlayerId = world.PlayerId,
                ScanAssemblies = new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Demo.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly,
                },
                NamespacePrefixes = new[] { "AbilityKit" },
                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };
        }
    }

    internal sealed class DefaultBattleReplaySessionRuntime : IBattleReplaySessionRuntime
    {
        private readonly IBattleLogicSessionRegistry _registry;
        private readonly BattleLogicSession _session;
        private readonly FrameReplayDriver _driver;
        private bool _disposed;

        public DefaultBattleReplaySessionRuntime(
            IBattleLogicSessionRegistry registry,
            BattleLogicSession session,
            FrameReplayDriver driver)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _driver.SeekToStart();
        }

        public bool IsActive => !_disposed && ReferenceEquals(_registry.Current, _session);
        public bool IsPlaying => !_disposed && _driver.IsPlaying;
        public int LastFrame => _driver.LastFrame;

        public float PlaybackSpeed
        {
            get => _driver.PlaybackSpeed;
            set => _driver.PlaybackSpeed = value;
        }

        public void Play()
        {
            ThrowIfDisposed();
            _driver.Play();
        }

        public void Pause()
        {
            ThrowIfDisposed();
            _driver.Pause();
        }

        public void PumpAndTick(int frame, float fixedDeltaSeconds)
        {
            ThrowIfDisposed();
            _driver.PumpFrame(_session, frame);
            _session.Tick(fixedDeltaSeconds);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.Stop();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DefaultBattleReplaySessionRuntime));
        }
    }
}
