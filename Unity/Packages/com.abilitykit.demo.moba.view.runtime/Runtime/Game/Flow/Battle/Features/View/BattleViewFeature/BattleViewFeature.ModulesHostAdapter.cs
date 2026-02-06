using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.View;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        BattleContext IViewFeatureModulesHost.Context => _ctx;
        BattleViewBinder IViewFeatureModulesHost.Binder => _binder;
        BattleEventBus IViewFeatureModulesHost.Events => _events;
        bool IViewFeatureModulesHost.IsConfirmed => false;
        WorldId IViewFeatureModulesHost.WorldId => _ctx != null ? _ctx.RuntimeWorldId : default;

        void IViewFeatureModulesHost.RefreshDirtyViews() => RefreshDirtyViews();
        void IViewFeatureModulesHost.RegisterAllSeekables() => RegisterAllSeekables();
        void IViewFeatureModulesHost.SeekAllToCurrentFrame() => SeekAllToCurrentFrame();

        void IViewFeatureModulesHost.RebindAllViews()
        {
            if (_ctx?.EntityWorld == null) return;
            _binder?.RebindAll(_ctx.EntityWorld);
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
