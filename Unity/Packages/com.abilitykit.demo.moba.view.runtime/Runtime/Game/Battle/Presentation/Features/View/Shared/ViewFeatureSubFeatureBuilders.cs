using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewFeatureSubFeatureBuilder
    {
        private readonly ViewSubFeatureModuleFactory _modules;

        public ViewFeatureSubFeatureBuilder(ViewSubFeatureModuleFactory modules = null)
        {
            _modules = modules ?? new ViewSubFeatureModuleFactory();
        }

        public void AddBattleViewSubFeatures(List<IViewSubFeature<BattleViewFeature>> subFeatures)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            AddModules(subFeatures, _modules.CreateDefaultModules<BattleViewFeature>());
        }

        public void AddConfirmedViewSubFeatures(List<IViewSubFeature<ConfirmedBattleViewFeature>> subFeatures)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            AddModules(subFeatures, _modules.CreateDefaultModules<ConfirmedBattleViewFeature>());
        }

        public void AddConfirmedViewSubFeatures(
            List<IViewSubFeature<ConfirmedBattleViewFeature>> subFeatures,
            BattleProjectionViewCapabilities capabilities, bool includeBuiltInEventAdapters)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));
            AddModules(subFeatures,
                _modules.CreateModules<ConfirmedBattleViewFeature>(capabilities, includeBuiltInEventAdapters));
        }

        private static void AddModules<TFeature>(List<IViewSubFeature<TFeature>> subFeatures,
            IReadOnlyList<IViewSubFeatureModule<TFeature>> modules)
            where TFeature : class, IViewFeatureRuntime
        {
            for (var i = 0; i < modules.Count; i++)
            {
                modules[i].AddTo(subFeatures);
            }
        }
    }

    internal interface IViewSubFeatureModule<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        string Name { get; }

        void AddTo(List<IViewSubFeature<TFeature>> subFeatures);
    }

    internal sealed class ViewSubFeatureModuleFactory
    {
        public IReadOnlyList<IViewSubFeatureModule<TFeature>> CreateDefaultModules<TFeature>()
            where TFeature : class, IViewFeatureRuntime
            => CreateModules<TFeature>(BattleProjectionViewCapabilities.Full,
                includeBuiltInEventAdapters: true);

        public IReadOnlyList<IViewSubFeatureModule<TFeature>> CreateModules<TFeature>(
            BattleProjectionViewCapabilities capabilities, bool includeBuiltInEventAdapters)
            where TFeature : class, IViewFeatureRuntime
        {
            return new IViewSubFeatureModule<TFeature>[]
            {
                new ViewRuntimeSubFeatureModule<TFeature>(capabilities),
                new ViewPresentationSubFeatureModule<TFeature>(capabilities),
                new ViewEventSubFeatureModule<TFeature>(capabilities, includeBuiltInEventAdapters),
            };
        }
    }

    internal sealed class ViewRuntimeSubFeatureModule<TFeature> : IViewSubFeatureModule<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly BattleProjectionViewCapabilities _capabilities;

        public ViewRuntimeSubFeatureModule(
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Full)
        {
            _capabilities = capabilities;
        }

        public string Name => "ViewRuntime";

        public void AddTo(List<IViewSubFeature<TFeature>> subFeatures)
        {
            subFeatures.Add(new ViewContextBindingSubFeature<TFeature>());
            subFeatures.Add(new ViewTimelineSubFeature<TFeature>());
            if ((_capabilities & BattleProjectionViewCapabilities.Vfx) != 0)
                subFeatures.Add(new ViewVfxSubFeature<TFeature>());
            subFeatures.Add(new ViewBindingSubFeature<TFeature>());
        }
    }

    internal sealed class ViewPresentationSubFeatureModule<TFeature> : IViewSubFeatureModule<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly BattleProjectionViewCapabilities _capabilities;

        public ViewPresentationSubFeatureModule(
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Full)
        {
            _capabilities = capabilities;
        }

        public string Name => "ViewPresentation";

        public void AddTo(List<IViewSubFeature<TFeature>> subFeatures)
        {
            if ((_capabilities & BattleProjectionViewCapabilities.FloatingText) != 0)
                subFeatures.Add(new ViewFloatingTextSubFeature<TFeature>());
            if ((_capabilities & BattleProjectionViewCapabilities.AreaEffects) != 0)
                subFeatures.Add(new ViewAreaViewsSubFeature<TFeature>());
        }
    }

    internal sealed class ViewEventSubFeatureModule<TFeature> : IViewSubFeatureModule<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly BattleProjectionViewCapabilities _capabilities;
        private readonly bool _includeBuiltInEventAdapters;

        public ViewEventSubFeatureModule(
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Full,
            bool includeBuiltInEventAdapters = true)
        {
            _capabilities = capabilities;
            _includeBuiltInEventAdapters = includeBuiltInEventAdapters;
        }

        public string Name => "ViewEvents";

        public void AddTo(List<IViewSubFeature<TFeature>> subFeatures)
        {
            if ((_capabilities & BattleProjectionViewCapabilities.Events) == 0) return;
            subFeatures.Add(new ViewEventSinkSubFeature<TFeature>());
            if (_includeBuiltInEventAdapters)
                subFeatures.Add(new ViewEventAdaptersSubFeature<TFeature>());
        }
    }
}
