using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game
{
    public interface IEntityCreator
    {
        EC.Entity Create();
        EC.Entity Create(string debugName);

        EC.Entity CreateChild(EC.Entity parent);
        EC.Entity CreateChild(EC.Entity parent, string debugName);
        EC.Entity CreateChild(EC.Entity parent, int childId);
        EC.Entity CreateChild(EC.Entity parent, int childId, string debugName);
    }
}
