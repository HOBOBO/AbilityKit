using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
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
        bool TryPickHero(PlayerId playerId, int heroId, int attributeTemplateId = 0, int level = 1, int basicAttackSkillId = 0, int[] skillIds = null);

        bool TrySetSpawnPoint(PlayerId playerId, int spawnPointId);

        bool TryBuildGameStartSpec(PlayerId localPlayerId, out MobaGameStartSpec spec);

        bool TryBuildRoomGameStartSpec(out MobaRoomGameStartSpec spec);

        MobaRoomCommandResult Apply(in MobaRoomCommand command);
    }
}
