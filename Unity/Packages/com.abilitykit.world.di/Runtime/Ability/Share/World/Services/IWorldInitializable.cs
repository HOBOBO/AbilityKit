using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Services
{
    public interface IWorldInitializable : IService
    {
        void OnInit(IWorldServices services);
    }
}
