using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private void AddFeatureSubFeatures(List<IViewSubFeature<BattleViewFeature>> subFeatures)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            subFeatures.Add(new ContextBindingSubFeature());
            subFeatures.Add(new TimelineSubFeature());
            subFeatures.Add(new VfxSubFeature());
            subFeatures.Add(new BindingSubFeature());
            subFeatures.Add(new FloatingTextSubFeature());
            subFeatures.Add(new AreaViewsSubFeature());
            subFeatures.Add(new EventSinkSubFeature());
            subFeatures.Add(new EventAdaptersSubFeature());
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private void AddFeatureSubFeatures(List<IViewSubFeature<ConfirmedBattleViewFeature>> subFeatures)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            subFeatures.Add(new ContextBindingSubFeature());
            subFeatures.Add(new TimelineSubFeature());
            subFeatures.Add(new VfxSubFeature());
            subFeatures.Add(new BindingSubFeature());
            subFeatures.Add(new FloatingTextSubFeature());
            subFeatures.Add(new AreaViewsSubFeature());
            subFeatures.Add(new EventSinkSubFeature());
            subFeatures.Add(new EventAdaptersSubFeature());
        }
    }
}
