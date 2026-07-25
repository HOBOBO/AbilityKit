using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Modules;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewSubFeaturePipeline
    {
        internal void AddStandardViewSubFeatures<TFeature>(List<IViewSubFeature<TFeature>> subFeatures)
            where TFeature : class, IViewSharedSubFeatureHost
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            subFeatures.Add(new SharedDirtySyncSubFeature<TFeature>());
            subFeatures.Add(new SharedTimelineSubFeature<TFeature>());
            subFeatures.Add(new SharedInterpolationSubFeature<TFeature>());
            subFeatures.Add(new SharedVfxTickSubFeature<TFeature>());
            subFeatures.Add(new SharedProjectileTickSubFeature<TFeature>());
            subFeatures.Add(new SharedFloatingTextSubFeature<TFeature>());
        }

        internal ModuleHost<FeatureModuleContext<TFeature>, IViewSubFeature<TFeature>> CreateHost<TFeature>(
            List<IViewSubFeature<TFeature>> subFeatures,
            Action<string> fail = null)
            where TFeature : class, IViewSharedSubFeatureHost
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            fail ??= message => Log.Error($"[ViewSubFeaturePipeline:{typeof(TFeature).Name}] {message}");

            return new ModuleHost<FeatureModuleContext<TFeature>, IViewSubFeature<TFeature>>(
                subFeatures,
                fail: fail);
        }
    }
}
