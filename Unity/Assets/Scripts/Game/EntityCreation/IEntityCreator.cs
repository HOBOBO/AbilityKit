using AbilityKit.Ability.EC;

namespace AbilityKit.Game
{
    public interface IEntityCreator
    {
        Entity Create();
        Entity Create(string debugName);

        Entity CreateChild(Entity parent);
        Entity CreateChild(Entity parent, string debugName);
        Entity CreateChild(Entity parent, int childId);
        Entity CreateChild(Entity parent, int childId, string debugName);
    }
}
