using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface IViewSharedSubFeatureHost
    {
        IBattleRuntimeContext RuntimeContext { get; }
        IBattleEntityContext EntityContext { get; }
        BattleViewBinder Binder { get; }

        bool IsConfirmed { get; }
        WorldId WorldId { get; }

        void RefreshDirtyViews();
        void RegisterAllSeekables();
        void SeekAllToCurrentFrame();
        void RebindAllViews();
        void TickVfx();
        void TickFloatingTexts(float deltaTime);
    }

    internal interface IViewSubFeature<TFeature> :
        IGameModule<FeatureModuleContext<TFeature>>,
        IGameModuleTick<FeatureModuleContext<TFeature>>,
        IGameModuleRebind<FeatureModuleContext<TFeature>>
        where TFeature : class, IViewSharedSubFeatureHost
    {
    }
}
