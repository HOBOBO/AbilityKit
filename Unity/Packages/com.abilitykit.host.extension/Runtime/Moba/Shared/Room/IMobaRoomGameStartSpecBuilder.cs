using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public interface IMobaRoomGameStartSpecBuilder
    {
        bool TryBuild(MobaRoomState state, out MobaRoomGameStartSpec spec);
    }
}
