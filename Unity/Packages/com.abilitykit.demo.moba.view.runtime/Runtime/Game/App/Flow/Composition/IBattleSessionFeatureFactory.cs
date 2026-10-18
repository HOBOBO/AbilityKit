using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public interface IBattleSessionFeatureFactory
    {
        IBattleSessionFeature Create(
            IBattleBootstrapper bootstrapper,
            Func<BattleStartPlan, IConnection> gatewayConnectionFactory);
    }

    public interface IBattleSessionModuleCatalog
    {
        bool ContainsModule(string moduleId);
    }

    public interface IBattleSessionModuleCapabilityCatalog
    {
        IReadOnlyList<string> GetCapabilities(string moduleId);
    }

    public sealed class DefaultBattleSessionFeatureFactory : IBattleSessionFeatureFactory,
        IBattleSessionModuleCatalog, IBattleSessionModuleCapabilityCatalog
    {
        public bool ContainsModule(string moduleId) => SessionSubFeaturePipeline.IsBuiltIn(moduleId);
        public IReadOnlyList<string> GetCapabilities(string moduleId) =>
            MobaBattleFlowProfile.GetBuiltInModuleCapabilities(moduleId);

        public IBattleSessionFeature Create(
            IBattleBootstrapper bootstrapper,
            Func<BattleStartPlan, IConnection> gatewayConnectionFactory)
        {
            return new BattleSessionFeature(bootstrapper, gatewayConnectionFactory);
        }
    }
}
