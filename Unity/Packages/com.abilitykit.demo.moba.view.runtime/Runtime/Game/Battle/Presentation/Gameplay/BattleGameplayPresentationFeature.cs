using System;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleGameplayPresentationFeature : IGamePhaseFeature
    {
        private readonly GameplayPresentationProfileCatalog _profiles;
        private readonly GameplayPresentationModuleRegistry _modules;
        private IBattleGameplayPresentationSource _context;
        private BattleGameplayPresentationState _state;
        private FrameSnapshotDispatcher _dispatcher;
        private bool _hasBoundDispatcher;
        private IDisposable _clockSubscription;
        private IGameplayProjectionModule _projection;

        public BattleGameplayPresentationFeature()
            : this(null, null)
        {
        }

        internal BattleGameplayPresentationFeature(
            GameplayPresentationProfileCatalog profiles = null,
            GameplayPresentationModuleRegistry modules = null)
        {
            _profiles = profiles ?? GameplayPresentationProfileCatalog.Default;
            _modules = modules ?? GameplayPresentationModuleRegistry.Default;
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            _context = ctx.Features.TryGet(out IBattleGameplayPresentationSource source)
                ? source
                : ctx.RequireFeature<BattleContext>(nameof(BattleGameplayPresentationFeature));
            _state = ctx.GameplayPresentation;
            TryBind();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            TryBind();
        }

        private void TryBind()
        {
            if (_state == null || _context == null) return;
            if (!_context.TryGetFrameSnapshots(out var snapshots))
            {
                if (_dispatcher != null) ClearSubscriptions();
                return;
            }
            if (ReferenceEquals(_dispatcher, snapshots) && _projection != null) return;
            ClearSubscriptions();
            _dispatcher = snapshots;
            var launch = _context.LaunchSpec;
            if (_hasBoundDispatcher && !IsSameMatch(launch.GameplayId, launch.MatchId))
                _state.Reset();
            if (launch.GameplayId > 0)
            {
                _state.SetLaunch(launch.GameplayId, launch.Players,
                    _context.ResolveLocalControlPlayerId(), launch.TickRate, launch.MatchId);
                var profile = _profiles.Get(launch.GameplayId);
                _state.SelectModules(launch.GameplayId, profile.HudModuleId,
                    profile.ProjectionModuleId, profile.SettlementModuleId);
            }
            _hasBoundDispatcher = true;
            _clockSubscription = snapshots.Subscribe<GameplayClockData>(
                GameplayClockSnapshotRoute.OpCode, (_, value) => _state.Apply(in value));
            if (launch.GameplayId > 0)
            {
                _projection = _modules.CreateProjection(_state.ProjectionModuleId);
                _projection.Bind(snapshots, _state);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            ClearSubscriptions();
            _context = null;
            _state = null;
            _hasBoundDispatcher = false;
        }

        private bool IsSameMatch(int gameplayId, string matchId) =>
            gameplayId > 0 && gameplayId == _state.GameplayId &&
            !string.IsNullOrEmpty(matchId) &&
            string.Equals(matchId, _state.MatchId, StringComparison.Ordinal);

        private void ClearSubscriptions()
        {
            _clockSubscription?.Dispose();
            _projection?.Dispose();
            _clockSubscription = null;
            _projection = null;
            _dispatcher = null;
        }
    }
}
