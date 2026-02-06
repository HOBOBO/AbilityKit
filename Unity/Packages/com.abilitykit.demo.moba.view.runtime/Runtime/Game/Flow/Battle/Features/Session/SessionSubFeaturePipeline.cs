using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal static class SessionSubFeaturePipeline
        {
            internal static void AddStandardSessionSubFeatures(List<ISessionSubFeature<BattleSessionFeature>> subFeatures)
            {
                if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

                subFeatures.Add(new SessionEventsSubFeature());
                subFeatures.Add(new SessionDispatchersSubFeature());
                subFeatures.Add(new SessionEditorHooksSubFeature());
                subFeatures.Add(new SessionLifecycleSubFeature());
                subFeatures.Add(new SessionNetAdapterSubFeature());
                subFeatures.Add(new SessionReplaySubFeature());
            }

            internal static void AddLegacySessionModules(
                List<ISessionSubFeature<BattleSessionFeature>> subFeatures,
                List<IBattleSessionModule> raw)
            {
                if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));
                if (raw == null) throw new ArgumentNullException(nameof(raw));

                for (int i = 0; i < raw.Count; i++)
                {
                    if (raw[i] == null) continue;
                    subFeatures.Add(SessionSubFeatureFactory.CreateLegacySubFeature(raw[i]));
                }

            }

            internal static void AddPostLegacySessionSubFeatures(List<ISessionSubFeature<BattleSessionFeature>> subFeatures)
            {
                if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

                subFeatures.Add(new SessionTickLoopSubFeature());
                subFeatures.Add(new SessionPlanSubFeature());
            }

            internal static ModuleHost<FeatureModuleContext<BattleSessionFeature>, ISessionSubFeature<BattleSessionFeature>> CreateModuleHost(
                List<ISessionSubFeature<BattleSessionFeature>> subFeatures,
                Action<string> fail)
            {
                if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

                fail ??= message => Log.Error($"[SessionSubFeaturePipeline] {message}");

                return new ModuleHost<FeatureModuleContext<BattleSessionFeature>, ISessionSubFeature<BattleSessionFeature>>(
                    subFeatures,
                    fail);
            }
        }
    }
}
