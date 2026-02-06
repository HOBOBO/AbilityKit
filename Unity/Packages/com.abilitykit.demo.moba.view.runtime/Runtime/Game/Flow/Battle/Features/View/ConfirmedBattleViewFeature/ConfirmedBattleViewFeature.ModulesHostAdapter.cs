using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.View;

namespace AbilityKit.Game.Flow
{
    public sealed partial class ConfirmedBattleViewFeature
    {
        BattleContext IViewFeatureModulesHost.Context => _confirmedCtx;
        BattleViewBinder IViewFeatureModulesHost.Binder => _binder;
        BattleEventBus IViewFeatureModulesHost.Events => _events;
        bool IViewFeatureModulesHost.IsConfirmed => true;
        WorldId IViewFeatureModulesHost.WorldId => _confirmedCtx != null ? _confirmedCtx.RuntimeWorldId : default;

        void IViewFeatureModulesHost.RefreshDirtyViews() => RefreshDirtyViews();
        void IViewFeatureModulesHost.RegisterAllSeekables() => RegisterAllSeekables();
        void IViewFeatureModulesHost.SeekAllToCurrentFrame() => SeekAllToCurrentFrame();

        void IViewFeatureModulesHost.RebindAllViews()
        {
            if (_confirmedCtx?.EntityWorld == null) return;
            _binder?.RebindAll(_confirmedCtx.EntityWorld);
        }

        void IViewFeatureModulesHost.TickVfx()
        {
            if (_vfxNode.IsValid) _vfx?.Tick(_vfxNode);
        }

        void IViewFeatureModulesHost.TickFloatingTexts(float deltaTime)
        {
            _floatingTexts?.Tick(deltaTime);
        }
    }
}
