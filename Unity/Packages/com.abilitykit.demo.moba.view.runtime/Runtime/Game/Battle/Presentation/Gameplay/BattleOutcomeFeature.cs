using System;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleOutcomeFeature : IGamePhaseFeature
    {
        private IBattleGameplayPresentationSource _context;
        private BattleOutcomeState _outcome;
        private AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher _dispatcher;
        private IDisposable _subscription;

        public void OnAttach(in GamePhaseContext ctx)
        {
            _context = ctx.RequireFeature<IBattleGameplayPresentationSource>(nameof(BattleOutcomeFeature));
            _outcome = ctx.BattleOutcome ?? throw new InvalidOperationException("Battle outcome state is required.");
            TryBind();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime) => TryBind();

        public void OnDetach(in GamePhaseContext ctx)
        {
            _subscription?.Dispose();
            _subscription = null;
            _dispatcher = null;
            _context = null;
            _outcome = null;
        }

        private void TryBind()
        {
            if (_context == null || _outcome == null) return;
            var launch = _context.LaunchSpec;
            _outcome.SetIdentity(launch.GameplayId, launch.MatchId);
            var snapshots = _context.TryGetFrameSnapshots(out var current) ? current : null;
            if (ReferenceEquals(snapshots, _dispatcher)) return;
            _subscription?.Dispose();
            _subscription = null;
            _dispatcher = snapshots;
            if (snapshots != null)
                _subscription = snapshots.Subscribe<GameplayMatchData>(
                    GameplayMatchSnapshotRoute.OpCode, (_, match) => _outcome.Apply(in match));
        }
    }
}
