using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Entitas
{
    public sealed class EntitasActorIdKeyProvider : IEntityKeyProvider
    {
        public ulong GetKey(EcsEntityId id)
        {
            return (ulong)id.ActorId;
        }
    }
}
