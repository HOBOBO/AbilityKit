using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public sealed class DefaultMobaRoomGameStartSpecBuilder : IMobaRoomGameStartSpecBuilder
    {
        public bool TryBuild(MobaRoomState state, out MobaRoomGameStartSpec spec)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.CanStart())
            {
                spec = default;
                return false;
            }

            var slots = new List<MobaRoomPlayerSlot>(state.Players.Count);
            var spawnFallback = 0;
            foreach (var kv in state.Players)
            {
                var s = kv.Value;
                var ov = new MobaRoomLoadoutOverrides(
                    level: s.Level,
                    attributeTemplateId: s.AttributeTemplateId,
                    basicAttackSkillId: s.BasicAttackSkillId,
                    skillIds: s.SkillIds);

                slots.Add(new MobaRoomPlayerSlot(
                    playerId: s.PlayerId,
                    teamId: s.TeamId,
                    heroId: s.HeroId,
                    spawnPointId: s.SpawnPointId > 0 ? s.SpawnPointId : spawnFallback,
                    overrides: in ov));

                spawnFallback++;
            }

            spec = new MobaRoomGameStartSpec(
                matchId: state.MatchId,
                mapId: state.MapId,
                randomSeed: state.RandomSeed,
                tickRate: state.TickRate,
                inputDelayFrames: state.InputDelayFrames,
                players: slots.Count == 0 ? null : slots.ToArray());
            return true;
        }
    }
}
