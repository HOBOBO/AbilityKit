using System.Collections.Generic;
using AbilityKit.Game.Flow.Battle.Modules;
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

    internal static class SessionSubFeatureFactory
    {
        internal static ISessionSubFeature<BattleSessionFeature> CreateLegacySubFeature(IBattleSessionModule inner)
        {
            return new LegacyBattleSessionModuleSubFeature(inner);
        }

        private sealed class LegacyBattleSessionModuleSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies,
            ISessionPlanSubFeature<BattleSessionFeature>,
            ISessionLifecycleSubFeature<BattleSessionFeature>,
            ISessionPreTickSubFeature<BattleSessionFeature>
        {
            private readonly IBattleSessionModule _inner;

            public LegacyBattleSessionModuleSubFeature(IBattleSessionModule inner)
            {
                _inner = inner;
            }

            public string Id => (_inner as IBattleSessionModuleId)?.Id;

            public IEnumerable<string> Dependencies => (_inner as IBattleSessionModuleDependencies)?.Dependencies;

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                _inner?.OnAttach(new BattleSessionModuleContext(ctx.Phase, ctx.Feature));
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                _inner?.OnDetach(new BattleSessionModuleContext(ctx.Phase, ctx.Feature));
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
            {
                _inner?.Tick(new BattleSessionModuleContext(ctx.Phase, ctx.Feature), deltaTime);
            }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
            }

            public bool OnPlanBuilt(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                if (_inner is IBattleSessionPlanModule plan)
                {
                    return plan.OnPlanBuilt(new BattleSessionModuleContext(ctx.Phase, ctx.Feature));
                }
                return false;
            }

            public void OnSessionStarting(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                if (_inner is IBattleSessionLifecycleModule lifecycle)
                {
                    lifecycle.OnSessionStarting(new BattleSessionModuleContext(ctx.Phase, ctx.Feature));
                }
            }

            public void OnSessionStopping(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                if (_inner is IBattleSessionLifecycleModule lifecycle)
                {
                    lifecycle.OnSessionStopping(new BattleSessionModuleContext(ctx.Phase, ctx.Feature));
                }
            }

            public void PreTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
            {
                if (_inner is IBattleSessionPreTickModule preTick)
                {
                    preTick.PreTick(new BattleSessionModuleContext(ctx.Phase, ctx.Feature), deltaTime);
                }
            }
        }
    }
}
