namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasActorIdKeyProvider : IEntityKeyProvider
    {
        public ulong GetKey(Battle.SearchTarget.EntityId id)
        {
            return id.Value;
        }
    }
}
