using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
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
