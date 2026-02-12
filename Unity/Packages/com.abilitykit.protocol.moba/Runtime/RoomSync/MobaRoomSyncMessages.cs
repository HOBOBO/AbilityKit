using AbilityKit.Ability.Host.Extensions.Moba.Room;
using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    [MemoryPackable]
    public readonly partial struct MobaRoomSnapshotMessage
    {
        [MemoryPackOrder(0)] public readonly MobaRoomSnapshot Snapshot;

        [MemoryPackConstructor]
        public MobaRoomSnapshotMessage(in MobaRoomSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaRoomCommandMessage
    {
        [MemoryPackOrder(0)] public readonly string ClientId;
        [MemoryPackOrder(1)] public readonly MobaRoomCommand Command;

        [MemoryPackConstructor]
        public MobaRoomCommandMessage(string clientId, in MobaRoomCommand command)
        {
            ClientId = clientId;
            Command = command;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaRoomCommandResultMessage
    {
        [MemoryPackOrder(0)] public readonly string ClientId;
        [MemoryPackOrder(1)] public readonly MobaRoomCommandResult Result;

        [MemoryPackConstructor]
        public MobaRoomCommandResultMessage(string clientId, in MobaRoomCommandResult result)
        {
            ClientId = clientId;
            Result = result;
        }
    }
}
