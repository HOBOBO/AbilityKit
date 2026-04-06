using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game
{
    public interface IEntityCreator
    {
        EC.IEntity Create();
        EC.IEntity Create(string debugName);

        EC.IEntity CreateChild(EC.IEntity parent);
        EC.IEntity CreateChild(EC.IEntity parent, string debugName);
        EC.IEntity CreateChild(EC.IEntity parent, int childId);
        EC.IEntity CreateChild(EC.IEntity parent, int childId, string debugName);
    }
}
