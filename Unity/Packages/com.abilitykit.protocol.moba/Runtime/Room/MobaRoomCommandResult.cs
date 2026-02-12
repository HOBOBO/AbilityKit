using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public enum MobaRoomCommandError
    {
        None = 0,
        InvalidCommand = 1,
        InvalidPlayerId = 2,
        StaleRevision = 3,
        PlayerAlreadyExists = 4,
        PlayerNotFound = 5,
        RoomFull = 6,
        InvalidHeroId = 7,
        InvalidSpawnPointId = 8,
    }

    [MemoryPackable]
    public readonly partial struct MobaRoomCommandResult
    {
        [MemoryPackOrder(0)] public readonly bool Ok;
        [MemoryPackOrder(1)] public readonly MobaRoomCommandError Error;
        [MemoryPackOrder(2)] public readonly int NewRevision;

        public MobaRoomCommandResult(bool ok, MobaRoomCommandError error, int newRevision)
        {
            Ok = ok;
            Error = error;
            NewRevision = newRevision;
        }

        public static MobaRoomCommandResult Success(int newRevision) => new MobaRoomCommandResult(true, MobaRoomCommandError.None, newRevision);
        public static MobaRoomCommandResult Fail(MobaRoomCommandError error, int newRevision) => new MobaRoomCommandResult(false, error, newRevision);
    }
}
