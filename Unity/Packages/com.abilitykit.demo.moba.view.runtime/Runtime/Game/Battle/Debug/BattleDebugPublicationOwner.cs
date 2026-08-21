using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleDebugPublicationOwner : IDisposable
    {
        private string _scope;
        private BattleContext _context;
        private BattleHudFeature _hud;
        private BattleViewFeature _view;
        private ConfirmedBattleViewFeature _confirmedView;

        public void Refresh(in GamePhaseContext phase)
        {
            if (phase.Features.TryGet(out BattleContext context) && context != null)
            {
                var nextScope = context.Plan.World.WorldId ?? string.Empty;
                if (!ReferenceEquals(context, _context) ||
                    !string.Equals(nextScope, _scope, StringComparison.Ordinal))
                {
                    WithdrawScopedPublications();
                    _context = context;
                    _scope = nextScope;
                    PublishScopedPublications();
                }

                BattleFlowDebugProvider.Current = context;
            }

            if (phase.Features.TryGet(out BattleHudFeature hud) &&
                hud != null &&
                !ReferenceEquals(hud, _hud))
            {
                BattleFlowDebugProvider.WithdrawHud(_scope, _hud);
                _hud = hud;
                BattleFlowDebugProvider.PublishHud(_scope, hud);
                BattleFlowDebugProvider.CurrentHud = hud;
            }

            if (phase.Features.TryGet(out BattleViewFeature view) &&
                view != null &&
                !ReferenceEquals(view, _view))
            {
                BattleFlowDebugProvider.WithdrawView(_scope, _view);
                _view = view;
                BattleFlowDebugProvider.PublishView(_scope, view);
                BattleFlowDebugProvider.CurrentView = view;
            }

            if (phase.Features.TryGet(out ConfirmedBattleViewFeature confirmedView) &&
                confirmedView != null &&
                !ReferenceEquals(confirmedView, _confirmedView))
            {
                BattleFlowDebugProvider.WithdrawConfirmedView(_scope, _confirmedView);
                _confirmedView = confirmedView;
                BattleFlowDebugProvider.PublishConfirmedView(_scope, confirmedView);
                BattleFlowDebugProvider.CurrentConfirmedView = confirmedView;
            }
        }

        private void WithdrawScopedPublications()
        {
            BattleFlowDebugProvider.WithdrawConfirmedView(_scope, _confirmedView);
            BattleFlowDebugProvider.WithdrawView(_scope, _view);
            BattleFlowDebugProvider.WithdrawHud(_scope, _hud);
            BattleFlowDebugProvider.WithdrawContext(_scope, _context);
        }

        private void PublishScopedPublications()
        {
            BattleFlowDebugProvider.PublishContext(_scope, _context);
            BattleFlowDebugProvider.PublishHud(_scope, _hud);
            BattleFlowDebugProvider.PublishView(_scope, _view);
            BattleFlowDebugProvider.PublishConfirmedView(_scope, _confirmedView);
        }

        public void Dispose()
        {
            WithdrawScopedPublications();

            if (ReferenceEquals(BattleFlowDebugProvider.Current, _context))
            {
                BattleFlowDebugProvider.Current = null;
            }

            if (ReferenceEquals(BattleFlowDebugProvider.CurrentHud, _hud))
            {
                BattleFlowDebugProvider.CurrentHud = null;
            }

            if (ReferenceEquals(BattleFlowDebugProvider.CurrentView, _view))
            {
                BattleFlowDebugProvider.CurrentView = null;
            }

            if (ReferenceEquals(BattleFlowDebugProvider.CurrentConfirmedView, _confirmedView))
            {
                BattleFlowDebugProvider.CurrentConfirmedView = null;
            }

            _scope = string.Empty;
            _confirmedView = null;
            _view = null;
            _hud = null;
            _context = null;
        }
    }
}
