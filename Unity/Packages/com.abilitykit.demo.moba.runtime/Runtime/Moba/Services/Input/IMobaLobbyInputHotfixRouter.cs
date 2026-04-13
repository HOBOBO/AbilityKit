using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaLobbyInputHotfixRouter : IService
    {
        bool TryHandle(IWorldResolver services, FrameIndex frame, PlayerInputCommand cmd);
    }
}
