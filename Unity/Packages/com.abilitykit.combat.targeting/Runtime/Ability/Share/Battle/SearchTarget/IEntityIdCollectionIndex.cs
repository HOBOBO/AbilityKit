using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface IEntityIdCollectionIndex
    {
        bool ForEach<TConsumer>(int key, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer;
    }
}
