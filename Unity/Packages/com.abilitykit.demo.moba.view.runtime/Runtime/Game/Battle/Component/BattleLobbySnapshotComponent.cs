using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleLobbySnapshotComponent
    {
        public int Revision;
        public bool CanStart;
        public MobaRoomPlayerSnapshot[] Players;
    }
}
