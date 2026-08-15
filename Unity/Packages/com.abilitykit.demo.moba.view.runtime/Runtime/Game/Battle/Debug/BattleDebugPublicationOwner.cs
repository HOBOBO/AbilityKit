using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleDebugPublicationOwner : IDisposable
    {
        private BattleContext _context;
        private BattleHudFeature _hud;
        private BattleViewFeature _view;
        private ConfirmedBattleViewFeature _confirmedView;

        public void Refresh(in GamePhaseContext phase)
        {
            if (phase.Features.TryGet(out BattleContext context) &&
                context != null &&
                !ReferenceEquals(context, _context))
            {
                _context = context;
                BattleFlowDebugProvider.Current = context;
            }

            if (phase.Features.TryGet(out BattleHudFeature hud) &&
                hud != null &&
                !ReferenceEquals(hud, _hud))
            {
                _hud = hud;
                BattleFlowDebugProvider.CurrentHud = hud;
            }

            if (phase.Features.TryGet(out BattleViewFeature view) &&
                view != null &&
                !ReferenceEquals(view, _view))
            {
                _view = view;
                BattleFlowDebugProvider.CurrentView = view;
            }

            if (phase.Features.TryGet(out ConfirmedBattleViewFeature confirmedView) &&
                confirmedView != null &&
                !ReferenceEquals(confirmedView, _confirmedView))
            {
                _confirmedView = confirmedView;
                BattleFlowDebugProvider.CurrentConfirmedView = confirmedView;
            }
        }

        public void Dispose()
        {
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

            _confirmedView = null;
            _view = null;
            _hud = null;
            _context = null;
        }
    }
}
