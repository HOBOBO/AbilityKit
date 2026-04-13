using System;
using System.Linq;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaGameStartSpecNormalizer
    {
        public static EnterMobaGameReq Normalize(MobaConfigDatabase config, in EnterMobaGameReq req)
        {
            if (config == null) return req;
            if (req.Players == null || req.Players.Length == 0) return req;

            try
            {
                var src = req.Players;
                var dst = new MobaPlayerLoadout[src.Length];

                for (int i = 0; i < src.Length; i++)
                {
                    var p = src[i];

                    var attributeTemplateId = p.AttributeTemplateId;
                    int[] skillIds = p.SkillIds;

                    if ((attributeTemplateId <= 0 || skillIds == null) && config.TryGetCharacter(p.HeroId, out var character) && character != null)
                    {
                        if (attributeTemplateId <= 0)
                        {
                            attributeTemplateId = character.AttributeTemplateId;
                        }

                        if (skillIds == null)
                        {
                            // 从 AttributeTemplate 获取技能列表
                            if (attributeTemplateId > 0 && config.TryGetAttributeTemplate(attributeTemplateId, out var attrTemplate) && attrTemplate != null)
                            {
                                skillIds = attrTemplate.ActiveSkills?.ToArray() ?? Array.Empty<int>();
                            }
                            else
                            {
                                skillIds = Array.Empty<int>();
                            }
                        }
                    }

                    dst[i] = new MobaPlayerLoadout(
                        playerId: p.PlayerId,
                        teamId: p.TeamId,
                        heroId: p.HeroId,
                        attributeTemplateId: attributeTemplateId,
                        level: p.Level,
                        basicAttackSkillId: p.BasicAttackSkillId,
                        skillIds: skillIds,
                        spawnIndex: p.SpawnIndex,
                        unitSubType: p.UnitSubType,
                        mainType: p.MainType,
                        hasSpawnPosition: p.HasSpawnPosition,
                        spawnX: p.SpawnX,
                        spawnY: p.SpawnY,
                        spawnZ: p.SpawnZ);
                }

                return new EnterMobaGameReq(
                    playerId: req.PlayerId,
                    matchId: req.MatchId,
                    mapId: req.MapId,
                    randomSeed: req.RandomSeed,
                    tickRate: req.TickRate,
                    inputDelayFrames: req.InputDelayFrames,
                    opCode: req.OpCode,
                    payload: req.Payload,
                    players: dst);
            }
            catch
            {
                return req;
            }
        }
    }
}
