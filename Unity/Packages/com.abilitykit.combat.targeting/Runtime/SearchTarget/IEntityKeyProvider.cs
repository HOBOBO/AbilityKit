namespace AbilityKit.Battle.SearchTarget
{
    public interface IEntityKeyProvider
    {
        ulong GetKey(AbilityKit.Ability.Share.ECS.EcsEntityId id);
    }
}
