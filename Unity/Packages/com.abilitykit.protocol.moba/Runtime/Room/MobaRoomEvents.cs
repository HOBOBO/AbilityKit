using System;
using AbilityKit.Ability.Host;
using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public enum MobaRoomChangeKind
    {
        Unknown = 0,
        PlayerJoined = 1,
        PlayerLeft = 2,
        ReadyChanged = 3,
        HeroPicked = 4,
        SpawnPointChanged = 5,
        ConfigChanged = 6,
    }

    [MemoryPackable]
    public readonly partial struct MobaRoomChangedArgs
    {
        [MemoryPackOrder(0)] public readonly MobaRoomChangeKind Kind;
        [MemoryPackOrder(1)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(2)] public readonly int Revision;

        [MemoryPackConstructor]
        public MobaRoomChangedArgs(MobaRoomChangeKind kind, PlayerId playerId, int revision)
        {
            Kind = kind;
            PlayerId = playerId;
            Revision = revision;
        }
    }
}
