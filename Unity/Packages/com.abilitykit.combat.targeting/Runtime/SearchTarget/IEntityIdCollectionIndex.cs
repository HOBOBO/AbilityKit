using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface IEntityIdCollectionIndex
    {
        bool ForEach<TConsumer>(int key, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer;
    }
}
