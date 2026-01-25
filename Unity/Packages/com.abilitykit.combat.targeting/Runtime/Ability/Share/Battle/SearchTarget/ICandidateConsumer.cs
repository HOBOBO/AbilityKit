using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface ICandidateConsumer
    {
        void Consume(EcsEntityId id);
    }
}
