using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private IReadOnlyList<string> _sessionModuleIds = SessionSubFeaturePipeline.StandardModuleIds;
        private Func<string, IReadOnlyList<string>> _sessionModuleCapabilities =
            MobaBattleFlowProfile.GetBuiltInModuleCapabilities;
        private readonly Func<string, ISessionSubFeature<ISessionRuntimeFeature>> _sessionModuleFactory;

        internal void ConfigureSessionModules(IReadOnlyList<string> moduleIds,
            Func<string, IReadOnlyList<string>> getCapabilities = null)
        {
            if (moduleIds == null) throw new ArgumentNullException(nameof(moduleIds));
            if (moduleIds.Count == 0) throw new ArgumentException("At least one session module is required.", nameof(moduleIds));
            if (_subFeatureHost != null || _featureLifecycle.State != BattleSessionFeatureLifecycleState.Idle)
                throw new InvalidOperationException("Session modules must be configured before attach.");
            var copy = new string[moduleIds.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = moduleIds[i];
            var modules = new List<ISessionSubFeature<ISessionRuntimeFeature>>(copy.Length);
            SessionSubFeaturePipeline.AddConfiguredSessionSubFeatures(
                modules, copy, _sessionModuleFactory);
            var host = SessionSubFeaturePipeline.CreateHost(
                modules, message => throw new InvalidOperationException(message));
            if (!host.TrySortByDependencies())
                throw new InvalidOperationException("Battle session module dependencies are invalid.");
            _sessionModuleIds = Array.AsReadOnly(copy);
            _sessionModuleCapabilities = getCapabilities ?? MobaBattleFlowProfile.GetBuiltInModuleCapabilities;
            _subFeatures = modules;
            _subFeatureHost = host;
        }

        private void EnsureSubFeaturesCreated()
        {
            _subFeatures ??= new List<ISessionSubFeature<ISessionRuntimeFeature>>(capacity: 8);
            if (_subFeatureHost != null && _subFeatures.Count > 0) return;

            void Fail(string message)
            {
                Log.Error($"[BattleSessionFeature] SubFeature dependency validation failed: {message}");

                var ex = new InvalidOperationException(message);
                if (_eventsCtrl != null)
                {
                    _eventsCtrl.NotifySessionFailed(this, ex);
                }
                else
                {
                    _pendingSubFeatureValidationFailure = ex;
                }
            }

            _subFeatureHost = SessionSubFeaturePipeline.CreateHost(_subFeatures, Fail);

            if (_subFeatures.Count == 0)
            {
                SessionSubFeaturePipeline.AddConfiguredSessionSubFeatures(
                    _subFeatures, _sessionModuleIds, _sessionModuleFactory);

                if (!_subFeatureHost.TrySortByDependencies())
                {
                    throw new InvalidOperationException("Battle session module dependencies are invalid.");
                }
            }
        }

    }
}
