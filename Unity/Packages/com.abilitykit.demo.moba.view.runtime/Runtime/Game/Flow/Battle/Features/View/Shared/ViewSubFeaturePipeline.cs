using System;
using System.Collections.Generic;
using AbilityKit.Game.Flow.Modules;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class ViewSubFeaturePipeline
    {
        internal static void AddStandardViewModules<TFeature>(List<IViewSubFeature<TFeature>> subFeatures)
            where TFeature : class, IViewFeatureModulesHost
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            subFeatures.Add(new SharedDirtySyncModule<TFeature>());
            subFeatures.Add(new SharedTimelineModule<TFeature>());
            subFeatures.Add(new SharedInterpolationModule<TFeature>());
            subFeatures.Add(new SharedVfxTickModule<TFeature>());
            subFeatures.Add(new SharedFloatingTextModule<TFeature>());
        }

        internal static ModuleHost<FeatureModuleContext<TFeature>, IViewSubFeature<TFeature>> CreateModuleHost<TFeature>(
            List<IViewSubFeature<TFeature>> subFeatures,
            Action<string> fail = null)
            where TFeature : class, IViewFeatureModulesHost
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            fail ??= message => Debug.LogError($"[ViewSubFeaturePipeline:{typeof(TFeature).Name}] {message}");

            return new ModuleHost<FeatureModuleContext<TFeature>, IViewSubFeature<TFeature>>(
                subFeatures,
                fail: fail);
        }
    }
}
