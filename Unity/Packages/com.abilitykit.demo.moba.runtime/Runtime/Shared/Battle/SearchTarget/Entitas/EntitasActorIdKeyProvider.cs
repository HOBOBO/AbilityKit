namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasActorIdKeyProvider : IEntityKeyProvider
    {
        public ulong GetKey(Battle.SearchTarget.IEntityId id)
        {
            return (ulong)id.ActorId;
        }
    }
}
