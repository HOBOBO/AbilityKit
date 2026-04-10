using AbilityKit.Ability.Share.ECS; using AbilityKit.ECS; using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasActorIdKeyProvider : IEntityKeyProvider
    {
        public ulong GetKey(EcsEntityId id)
        {
            return (ulong)id.ActorId;
        }
    }
}
