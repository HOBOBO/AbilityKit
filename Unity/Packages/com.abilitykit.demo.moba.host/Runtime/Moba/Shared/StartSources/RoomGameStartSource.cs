using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class RoomGameStartSource : IMobaGameStartSource
    {
        private readonly IMobaRoomOrchestrator _room;

        public static readonly MobaGameStartSourceKey SourceKey = new MobaGameStartSourceKey("room");

        public MobaGameStartSourceKey Key => SourceKey;

        public int Priority => 100;

        public RoomGameStartSource(IMobaRoomOrchestrator room)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            return _room.TryBuildRoomGameStartSpec(out spec);
        }
    }
}
