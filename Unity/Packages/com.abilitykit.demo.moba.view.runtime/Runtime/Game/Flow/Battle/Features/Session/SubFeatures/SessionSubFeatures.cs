using System.Collections.Generic;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Ability.Host;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionSubFeature<TFeature> :
        IGameModule<FeatureModuleContext<TFeature>>,
        IGameModuleTick<FeatureModuleContext<TFeature>>,
        IGameModuleRebind<FeatureModuleContext<TFeature>>
        where TFeature : class
    {
    }

    internal interface ISessionPlanSubFeature<TFeature>
        where TFeature : class
    {
        bool OnPlanBuilt(in FeatureModuleContext<TFeature> ctx);
    }

    internal interface ISessionLifecycleSubFeature<TFeature>
        where TFeature : class
    {
        void OnSessionStarting(in FeatureModuleContext<TFeature> ctx);
        void OnSessionStopping(in FeatureModuleContext<TFeature> ctx);
    }

    internal interface ISessionPreTickSubFeature<TFeature>
        where TFeature : class
    {
        void PreTick(in FeatureModuleContext<TFeature> ctx, float deltaTime);
    }

    internal interface ISessionMainTickSubFeature<TFeature>
        where TFeature : class
    {
        void MainTick(in FeatureModuleContext<TFeature> ctx, float deltaTime);
    }

    internal interface ISessionLifecycleNotifySubFeature<TFeature>
        where TFeature : class
    {
        void NotifySessionStarting(in FeatureModuleContext<TFeature> ctx);
        void NotifySessionStopping(in FeatureModuleContext<TFeature> ctx);
    }

    internal interface ISessionReplaySetupSubFeature<TFeature>
        where TFeature : class
    {
        void SetupReplayOrRecord(in FeatureModuleContext<TFeature> ctx);
    }

    internal interface ISessionFrameReceivedSubFeature<TFeature>
        where TFeature : class
    {
        void OnFrameReceived(in FeatureModuleContext<TFeature> ctx, FramePacket packet);
    }

    internal interface ISessionFramePacketTransformSubFeature<TFeature>
        where TFeature : class
    {
        FramePacket TransformFramePacket(in FeatureModuleContext<TFeature> ctx, FramePacket packet);
    }
}
