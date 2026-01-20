using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public interface IMobaLobbyInputHotfixRouter : IService
    {
        bool TryHandle(IWorldServices services, FrameIndex frame, PlayerInputCommand cmd);
    }
}
