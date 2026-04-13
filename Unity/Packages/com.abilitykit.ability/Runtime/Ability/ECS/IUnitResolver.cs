namespace AbilityKit.Ability.Share.ECS
{
    using AbilityKit.ECS;
    
    public interface IUnitResolver
    {
        bool TryResolve(EcsEntityId id, out IUnitFacade unit);
    }
}
