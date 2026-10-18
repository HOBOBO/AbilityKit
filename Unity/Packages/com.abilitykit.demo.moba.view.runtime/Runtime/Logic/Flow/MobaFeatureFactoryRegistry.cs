using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    public enum MobaFeatureLifetime
    {
        Phase,
        Battle
    }

    public sealed class MobaFeatureContract
    {
        public MobaFeatureContract(MobaFeatureLifetime lifetime = MobaFeatureLifetime.Phase,
            string[] requiredFeatureIds = null, Type[] requiredPorts = null, Type[] providedPorts = null,
            string[] satisfiedPortGroups = null)
        {
            if (lifetime != MobaFeatureLifetime.Phase && lifetime != MobaFeatureLifetime.Battle)
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            foreach (var id in requiredFeatureIds ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Required feature ids cannot be empty.", nameof(requiredFeatureIds));
            foreach (var port in requiredPorts ?? Array.Empty<Type>())
                if (port == null) throw new ArgumentException("Required ports cannot be null.", nameof(requiredPorts));
            foreach (var port in providedPorts ?? Array.Empty<Type>())
                if (port == null) throw new ArgumentException("Provided ports cannot be null.", nameof(providedPorts));
            foreach (var group in satisfiedPortGroups ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(group))
                    throw new ArgumentException("Satisfied port groups cannot be empty.", nameof(satisfiedPortGroups));
            Lifetime = lifetime;
            RequiredFeatureIds = Array.AsReadOnly((string[])(requiredFeatureIds ?? Array.Empty<string>()).Clone());
            RequiredPorts = Array.AsReadOnly((Type[])(requiredPorts ?? Array.Empty<Type>()).Clone());
            ProvidedPorts = Array.AsReadOnly((Type[])(providedPorts ?? Array.Empty<Type>()).Clone());
            SatisfiedPortGroups = Array.AsReadOnly((string[])(satisfiedPortGroups ?? Array.Empty<string>()).Clone());
        }

        public MobaFeatureLifetime Lifetime { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
        public IReadOnlyList<Type> RequiredPorts { get; }
        public IReadOnlyList<Type> ProvidedPorts { get; }
        /// <summary>
        /// 本 feature 能满足的玩法端口组。副本定义声明 <c>RequiredPortGroups</c> 后，
        /// 只有提供了这些端口组的 feature 才会被安装，因此不同副本可以分化出不同的 feature 组合。
        /// </summary>
        public IReadOnlyList<string> SatisfiedPortGroups { get; }

        public bool SatisfiesPortGroup(string portGroup) =>
            !string.IsNullOrWhiteSpace(portGroup) &&
            System.Linq.Enumerable.Contains(SatisfiedPortGroups, portGroup, StringComparer.Ordinal);
    }

    public sealed class MobaFeatureFactoryRegistry
    {
        private readonly Dictionary<string, PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>.FeatureFactory> _factories =
            new Dictionary<string, PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>.FeatureFactory>(StringComparer.Ordinal);
        private readonly Dictionary<string, MobaFeatureContract> _contracts =
            new Dictionary<string, MobaFeatureContract>(StringComparer.Ordinal);

        public MobaFeatureFactoryRegistry Register(
            string featureId,
            PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>.FeatureFactory factory,
            MobaFeatureContract contract = null)
        {
            if (string.IsNullOrWhiteSpace(featureId)) throw new ArgumentException("Feature id is required.", nameof(featureId));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (_factories.ContainsKey(featureId))
                throw new InvalidOperationException("Moba feature factory is already registered: " + featureId);
            _factories.Add(featureId, factory);
            _contracts.Add(featureId, contract ?? new MobaFeatureContract());
            return this;
        }

        public IGamePhaseFeature Create(string featureId, in GamePhaseContext ctx)
        {
            if (!_factories.TryGetValue(featureId, out var factory))
            {
                throw new InvalidOperationException($"Moba feature factory is not registered: {featureId}");
            }

            return factory(in ctx);
        }

        public bool Contains(string featureId) =>
            !string.IsNullOrEmpty(featureId) && _factories.ContainsKey(featureId);

        public MobaFeatureContract GetContract(string featureId) =>
            _contracts.TryGetValue(featureId, out var contract)
                ? contract
                : throw new InvalidOperationException("Moba feature contract is not registered: " + featureId);
    }
}
