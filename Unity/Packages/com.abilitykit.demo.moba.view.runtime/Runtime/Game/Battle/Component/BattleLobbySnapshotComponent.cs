using AbilityKit.Ability.Share.Impl.Moba.Services;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleLobbySnapshotComponent
    {
        public bool Started;
        public int Version;
        public PlayerReadyEntry[] Players;
    }
}
