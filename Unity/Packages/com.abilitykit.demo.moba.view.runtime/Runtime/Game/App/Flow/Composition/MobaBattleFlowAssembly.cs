using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaBattleFlowAssembly
    {
        private MobaBattleFlowAssembly(
            MobaBattleFlowComposition composition,
            MobaFlowConfiguration configuration,
            PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> featurePlan)
        {
            Composition = composition;
            Configuration = configuration;
            FeaturePlan = featurePlan;
        }

        public MobaBattleFlowComposition Composition { get; }
        public MobaFlowConfiguration Configuration { get; }
        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> FeaturePlan { get; }
        public string ProfileId => Composition.ProfileId;
        public IReadOnlyList<string> SessionModuleIds => Composition.SessionModuleIds;

        public static MobaBattleFlowAssembly Resolve(
            MobaBattleFlowProfile profile,
            MobaFlowExtensionRegistry extensions,
            MobaFeatureFactoryRegistry factories,
            MobaFeaturePlanFactory planFactory,
            Action<MobaBattleFlowComposition> validateSessionModules)
            => Resolve(profile, extensions, factories, planFactory, validateSessionModules, null);

        /// <summary>
        /// 按副本定义的玩法端口组装配。端口组决定本局安装哪些可选 feature，
        /// 因此不同副本可以有不同 feature 组合，而 profile 只描述基础能力。
        /// </summary>
        public static MobaBattleFlowAssembly Resolve(
            MobaBattleFlowProfile profile,
            MobaFlowExtensionRegistry extensions,
            MobaFeatureFactoryRegistry factories,
            MobaFeaturePlanFactory planFactory,
            Action<MobaBattleFlowComposition> validateSessionModules,
            Dungeons.MobaDungeonDef dungeon)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (factories == null) throw new ArgumentNullException(nameof(factories));
            if (planFactory == null) throw new ArgumentNullException(nameof(planFactory));
            if (validateSessionModules == null) throw new ArgumentNullException(nameof(validateSessionModules));

            var composition = profile.Resolve();
            if (dungeon != null && dungeon.RequiredPortGroups.Count > 0)
                composition = composition.FilterByPortGroups(dungeon.RequiredPortGroups, factories);
            composition.ValidateExtensions(extensions);
            validateSessionModules(composition);
            composition.ValidateFactories(factories);
            var configuration = MobaFlowConfiguration.CreateResolved(composition);
            var featurePlan = planFactory.CreateBattleFeaturePlan(composition);
            return new MobaBattleFlowAssembly(composition, configuration, featurePlan);
        }
    }
}
