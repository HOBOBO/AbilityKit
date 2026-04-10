using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface ICandidateConsumer
    {
        void Consume(EcsEntityId id);
    }
}
