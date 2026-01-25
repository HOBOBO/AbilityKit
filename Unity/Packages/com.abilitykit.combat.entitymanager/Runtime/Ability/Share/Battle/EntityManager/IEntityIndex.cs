namespace AbilityKit.Ability.Battle.EntityManager
{
    public interface IEntityIndex<TId>
    {
        void OnEntityAdded(TId id);
        void OnEntityRemoved(TId id);
        void OnEntityUpdated(TId id, EntityUpdate update);
    }
}
