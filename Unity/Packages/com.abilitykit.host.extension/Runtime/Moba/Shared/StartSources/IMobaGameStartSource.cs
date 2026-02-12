using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public enum MobaGameStartSourceKind
    {
        Unknown = 0,
        Room = 1,
        DungeonPreset = 2,
        Matchmaking = 3,
    }

    public interface IMobaGameStartSource
    {
        MobaGameStartSourceKind Kind { get; }

        bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec);
    }
}
