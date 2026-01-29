namespace AbilityKit.Ability.Share.ECS
{
    public interface IUnitResolver
    {
        bool TryResolve(EcsEntityId id, out IUnitFacade unit);
    }
}
