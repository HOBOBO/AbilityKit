using System;
using AbilityKit.Demo.Moba.Services.StateMachine;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// A profile-owned condition for a skill-slot candidate. It is evaluated
    /// against the same effective HFSM parameter contributions that drive
    /// character state transitions.
    /// </summary>
    public readonly struct MobaSkillSlotCandidateCondition
    {
        public const int None = 0;
        public const int Equal = 1;
        public const int NotEqual = 2;
        public const int Greater = 3;
        public const int GreaterOrEqual = 4;
        public const int Less = 5;
        public const int LessOrEqual = 6;

        public MobaSkillSlotCandidateCondition(int parameterId, int comparison, float expectedValue)
        {
            ParameterId = parameterId;
            Comparison = comparison;
            ExpectedValue = expectedValue;
            Validate();
        }

        public int ParameterId { get; }
        public int Comparison { get; }
        public float ExpectedValue { get; }
        public bool IsConditional => ParameterId > 0;

        public void Validate()
        {
            if (float.IsNaN(ExpectedValue) || float.IsInfinity(ExpectedValue))
                throw new ArgumentOutOfRangeException(nameof(ExpectedValue));
            if (ParameterId < 0)
                throw new ArgumentOutOfRangeException(nameof(ParameterId));
            if (ParameterId == 0)
            {
                if (Comparison != None)
                    throw new ArgumentException("An unconditional candidate must use comparison None.", nameof(Comparison));
                return;
            }
            if (Comparison < Equal || Comparison > LessOrEqual)
                throw new ArgumentOutOfRangeException(nameof(Comparison));
        }

        public bool Matches(int ownerActorId, MobaCharacterHfsmParameterService parameters)
        {
            Validate();
            if (!IsConditional) return true;
            if (parameters == null)
                throw new InvalidOperationException("A conditional skill-slot candidate requires the HFSM parameter service.");
            if (!parameters.IsDeclared(ownerActorId, ParameterId))
                throw new InvalidOperationException(
                    $"HFSM parameter {ParameterId} is not declared by actor {ownerActorId}'s profile.");

            var actual = parameters.Resolve(ownerActorId, ParameterId);
            const float epsilon = 0.0001f;
            switch (Comparison)
            {
                case Equal: return Math.Abs(actual - ExpectedValue) <= epsilon;
                case NotEqual: return Math.Abs(actual - ExpectedValue) > epsilon;
                case Greater: return actual > ExpectedValue;
                case GreaterOrEqual: return actual >= ExpectedValue;
                case Less: return actual < ExpectedValue;
                case LessOrEqual: return actual <= ExpectedValue;
                default: throw new InvalidOperationException("Unknown skill-slot candidate comparison.");
            }
        }
    }
}
