using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void EnsureModulesCreated()
        {
            _subFeatures ??= new List<ISessionSubFeature<BattleSessionFeature>>(capacity: 8);
            if (_subFeatureHost != null && _subFeatures.Count > 0) return;

            void Fail(string message)
            {
                Log.Error($"[BattleSessionFeature] Module dependency validation failed: {message}");

                var ex = new InvalidOperationException(message);
                if (Events != null)
                {
                    Events.Publish(new SessionFailedEvent(ex));
                    Events.Flush();
                }
                else
                {
                    _pendingModuleValidationFailure = ex;
                }
            }

            _subFeatureHost = SessionSubFeaturePipeline.CreateModuleHost(_subFeatures, Fail);

            if (_subFeatures.Count == 0)
            {
                SessionSubFeaturePipeline.AddStandardSessionSubFeatures(_subFeatures);
                SessionSubFeaturePipeline.AddLateSessionSubFeatures(_subFeatures);

                if (!_subFeatureHost.TrySortByDependencies())
                {
                    return;
                }
            }
        }

    }
}
