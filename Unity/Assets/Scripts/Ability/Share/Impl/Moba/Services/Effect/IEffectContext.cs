using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public interface IEffectContext : IAbilityPipelineContext
    {
        EffectContextKind Kind { get; }
        int SourceActorId { get; }
        int TargetActorId { get; }
        long SourceContextId { get; }

        bool TryGetSkill(out SkillContextView skill);
    }
}
