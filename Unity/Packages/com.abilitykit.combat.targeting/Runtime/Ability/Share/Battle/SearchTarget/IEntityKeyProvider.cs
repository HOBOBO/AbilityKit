namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface IEntityKeyProvider
    {
        ulong GetKey(AbilityKit.Ability.Share.ECS.EcsEntityId id);
    }
}
