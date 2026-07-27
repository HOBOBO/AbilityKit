using System;

namespace AbilityKit.Ability.World.DI
{
    public enum WorldServiceRegistrationPolicy
    {
        Replace,
        KeepExisting,
        Reject
    }

    public enum WorldServiceRegistrationOutcome
    {
        Added,
        Replaced,
        KeptExisting,
        Rejected
    }

    public enum WorldServiceOwnership
    {
        Container,
        External
    }

    public readonly struct WorldServiceRegistration
    {
        public WorldServiceRegistration(
            int sequence,
            Type serviceType,
            Type implementationType,
            WorldLifetime lifetime,
            WorldServiceOwnership ownership,
            WorldServiceRegistrationPolicy policy,
            WorldServiceRegistrationOutcome outcome,
            Type sourceModuleType,
            Type previousImplementationType,
            WorldServiceOwnership? previousOwnership,
            Type previousSourceModuleType)
        {
            Sequence = sequence;
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            ImplementationType = implementationType ?? serviceType;
            Lifetime = lifetime;
            Ownership = ownership;
            Policy = policy;
            Outcome = outcome;
            SourceModuleType = sourceModuleType;
            PreviousImplementationType = previousImplementationType;
            PreviousOwnership = previousOwnership;
            PreviousSourceModuleType = previousSourceModuleType;
        }

        public int Sequence { get; }
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public WorldLifetime Lifetime { get; }
        public WorldServiceOwnership Ownership { get; }
        public WorldServiceRegistrationPolicy Policy { get; }
        public WorldServiceRegistrationOutcome Outcome { get; }
        public Type SourceModuleType { get; }
        public Type PreviousImplementationType { get; }
        public WorldServiceOwnership? PreviousOwnership { get; }
        public Type PreviousSourceModuleType { get; }
    }
}
