using AbilityKit.Core.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    using AbilityKit.Ability;
    public interface IEffectContext : IAbilityPipelineContext
    {
        EffectContextKind Kind { get; }
        int SourceActorId { get; }
        int TargetActorId { get; }
        long SourceContextId { get; }

        bool TryGetSkill(out SkillContextView skill);
    }
}
