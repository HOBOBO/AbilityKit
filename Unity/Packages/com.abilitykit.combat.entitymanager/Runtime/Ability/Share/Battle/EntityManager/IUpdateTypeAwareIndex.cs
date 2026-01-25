namespace AbilityKit.Ability.Battle.EntityManager
{
    public interface IUpdateTypeAwareIndex<TId> : IEntityIndex<TId>
    {
        bool Accepts(int updateType);
    }
}
