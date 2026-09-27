using System;

namespace AbilityKit.Demo.Moba.Services
{
    [Flags]
    public enum MobaCombatExecutionFlags : uint
    {
        None = 0,
        PeriodicDamage = 1u << 0,
        SecondaryDamage = 1u << 1,
        ReflectedDamage = 1u << 2,
        ReflectionSuppressed = 1u << 3,
        LifestealSuppressed = 1u << 4,
    }

    public interface IMobaCombatExecutionFactsProvider
    {
        bool TryGetCombatExecutionFacts(out MobaCombatExecutionFacts facts);
    }

    /// <summary>
    /// Immutable gameplay facts carried by the authoritative combat context.
    /// Optional observers may inspect these facts, but never own or alter them.
    /// </summary>
    public readonly struct MobaCombatExecutionFacts : IEquatable<MobaCombatExecutionFacts>
    {
        public MobaCombatExecutionFacts(MobaCombatExecutionFlags flags)
        {
            if ((flags & MobaCombatExecutionFlags.ReflectedDamage) != 0)
            {
                flags |= MobaCombatExecutionFlags.SecondaryDamage
                         | MobaCombatExecutionFlags.ReflectionSuppressed;
            }

            Flags = flags;
        }

        public MobaCombatExecutionFlags Flags { get; }
        public bool IsPeriodicDamage => Has(MobaCombatExecutionFlags.PeriodicDamage);
        public bool IsSecondaryDamage => Has(MobaCombatExecutionFlags.SecondaryDamage);
        public bool IsReflectedDamage => Has(MobaCombatExecutionFlags.ReflectedDamage);
        public bool CanTriggerReflection => !Has(MobaCombatExecutionFlags.ReflectionSuppressed);
        public bool CanTriggerLifesteal => !Has(MobaCombatExecutionFlags.LifestealSuppressed);

        public bool Has(MobaCombatExecutionFlags flag)
        {
            return (Flags & flag) == flag;
        }

        public MobaCombatExecutionFacts AsReflectedDamage()
        {
            return new MobaCombatExecutionFacts(Flags | MobaCombatExecutionFlags.ReflectedDamage);
        }

        public bool Equals(MobaCombatExecutionFacts other) => Flags == other.Flags;
        public override bool Equals(object obj) => obj is MobaCombatExecutionFacts other && Equals(other);
        public override int GetHashCode() => (int)Flags;
        public static bool operator ==(MobaCombatExecutionFacts left, MobaCombatExecutionFacts right) => left.Equals(right);
        public static bool operator !=(MobaCombatExecutionFacts left, MobaCombatExecutionFacts right) => !left.Equals(right);

        public static MobaCombatExecutionFacts Resolve(object payload)
        {
            if (payload is MobaCombatExecutionContext context)
                return context.CombatFacts;
            if (payload is IMobaCombatExecutionFactsProvider provider
                && provider.TryGetCombatExecutionFacts(out var facts))
                return new MobaCombatExecutionFacts(facts.Flags);
            return default;
        }
    }

}
