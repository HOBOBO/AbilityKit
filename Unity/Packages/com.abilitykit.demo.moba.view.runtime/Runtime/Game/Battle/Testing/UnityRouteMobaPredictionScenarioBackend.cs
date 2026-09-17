using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Battle.Testing
{
    /// <summary>Socket-free host for the production Unity snapshot and BattleSyncFeature route.</summary>
    public sealed class UnityRouteMobaPredictionScenarioBackend : IMobaPredictionScenarioBackend
    {
        public string Id => MobaPredictionBackendIds.UnityRoute;

        public IMobaPredictionFrameRoute CreateRoute(
            IClientPredictionReconcileTarget target, WorldId worldId, int tickRate)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var context = BattleContext.Rent();
            BattleLogicSession session = null;
            try
            {
                session = new BattleLogicSession(new BattleLogicSessionOptions
                {
                    Mode = BattleLogicMode.Remote,
                    WorldId = worldId,
                    AutoConnect = false,
                    AutoCreateWorld = false,
                    AutoJoin = false,
                }, new NoopTransport());
                context.Plan = BattleStartPlanBuilder
                    .ForWorld(worldId.Value, "battle", "virtual-client", "p1", tickRate, 0)
                    .WithSync(BattleSyncMode.HybridPredictReconcile, BattleViewEventSourceMode.SnapshotOnly)
                    .Build();
                context.PredictionRuntime.Bind(null, target, null, null);
                return new Route(context, session, new MobaVirtualBattleSyncRoute(context, session));
            }
            catch
            {
                try { session?.Dispose(); }
                finally { BattleContext.Return(context); }
                throw;
            }
        }

        private sealed class Route : IMobaPredictionFrameRoute
        {
            private readonly BattleContext _context;
            private readonly BattleLogicSession _session;
            private readonly MobaVirtualBattleSyncRoute _sync;
            private bool _disposed;

            public Route(BattleContext context, BattleLogicSession session, MobaVirtualBattleSyncRoute sync)
            {
                _context = context;
                _session = session;
                _sync = sync;
            }

            public void Feed(FramePacket packet)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Route));
                _session.InjectRemoteFrame(packet);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try { _sync.Dispose(); }
                finally
                {
                    try { _session.Dispose(); }
                    finally { BattleContext.Return(_context); }
                }
            }
        }

        private sealed class NoopTransport : IBattleLogicTransport
        {
            public event Action<FramePacket> FramePushed { add { } remove { } }
            public void Connect() { }
            public void Disconnect() { }
            public void SendCreateWorld(CreateWorldRequest request) { }
            public void SendJoin(JoinWorldRequest request) { }
            public void SendLeave(LeaveWorldRequest request) { }
            public void SendInput(SubmitInputRequest request) { }
        }
    }
}
