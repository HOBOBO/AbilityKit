using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Gameplay
{
    public static class MobaGameplayFeatureIds
    {
        public const string MinionWaves = "minion_waves";
        public const string Structures = "structures";
    }

    public interface IMobaGameplayFeatureSet : IService
    {
        bool IsConfigured { get; }
        bool IsEnabled(string featureId);
        void Enable(string featureId);
        void Disable(string featureId);
        IReadOnlyCollection<string> EnabledFeatures { get; }
        void Reset();
    }

    /// <summary>
    /// Per-battle capability selection. It only gates optional gameplay modules;
    /// gameplay clocks, counters and outcomes remain owned by trigger modules.
    /// </summary>
    [WorldService(typeof(IMobaGameplayFeatureSet), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaGameplayFeatureSet), WorldLifetime.Scoped)]
    public sealed class MobaGameplayFeatureSet : IMobaGameplayFeatureSet
    {
        private readonly HashSet<string> _enabled = new HashSet<string>(StringComparer.Ordinal);

        public bool IsConfigured { get; private set; }
        public IReadOnlyCollection<string> EnabledFeatures => _enabled;

        public bool IsEnabled(string featureId)
        {
            return !string.IsNullOrWhiteSpace(featureId) && _enabled.Contains(featureId);
        }

        public void Enable(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("Gameplay feature id is required.", nameof(featureId));
            IsConfigured = true;
            _enabled.Add(featureId);
        }

        public void Disable(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId)) return;
            IsConfigured = true;
            _enabled.Remove(featureId);
        }

        public void Reset()
        {
            _enabled.Clear();
            IsConfigured = true;
        }

        public void Dispose()
        {
            _enabled.Clear();
            IsConfigured = false;
        }
    }
}
