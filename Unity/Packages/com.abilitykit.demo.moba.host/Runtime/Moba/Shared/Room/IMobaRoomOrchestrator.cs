using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public interface IMobaRoomOrchestrator : IMobaRoomEvents
    {
        MobaRoomState State { get; }

        MobaRoomSnapshot Snapshot { get; }

        bool TryJoin(PlayerId playerId, int teamId = 0);
        bool TryLeave(PlayerId playerId);
        bool TrySetReady(PlayerId playerId, bool ready);
        bool TryPickHero(PlayerId playerId, int heroId, int attributeTemplateId, int level, int basicAttackSkillId, int[] skillIds);

        bool TrySetSpawnPoint(PlayerId playerId, int spawnPointId);

        bool TryBuildGameStartSpec(PlayerId localPlayerId, out MobaGameStartSpec spec);

        bool TryBuildRoomGameStartSpec(out MobaRoomGameStartSpec spec);

        MobaRoomCommandResult Apply(in MobaRoomCommand command);
    }
}

