using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaGamePhaseService : IService
    {
        public bool InGame { get; private set; }

        public void SetInGame()
        {
            InGame = true;
        }

        public void Reset()
        {
            InGame = false;
        }

        public void Dispose()
        {
        }
    }
}
