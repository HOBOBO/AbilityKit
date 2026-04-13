using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Players Config", fileName = "BattlePlayersConfig")]
    public sealed class BattlePlayersConfigSO : ScriptableObject
    {
        [System.Serializable]
        public sealed class PlayerConfig
        {
            [LabelText("玩家ID")]
            public string PlayerId;

            [LabelText("队伍")]
            public Team TeamId = Team.Team1;

            [LabelText("主体类型")]
            public EntityMainType MainType = EntityMainType.Unit;

            [LabelText("单位子类型")]
            public UnitSubType UnitSubType = UnitSubType.Hero;

            [LabelText("英雄ID")]
            public int HeroId = 10001;

            [LabelText("属性模板ID")]
            public int AttributeTemplateId = 0;

            [LabelText("等级")]
            public int Level = 1;

            [LabelText("普攻技能ID")]
            public int BasicAttackSkillId = 1;

            [LabelText("技能ID列表")]
            public int[] SkillIds;

            [LabelText("出生点索引")]
            public int SpawnIndex = 0;

            [LabelText("出生点位置")]
            public Vector3 SpawnPosition = default;
        }

        [LabelText("本地玩家ID")]
        public string LocalPlayerId = "p1";

        [LabelText("队伍1玩家")]
        public List<PlayerConfig> Team1Players = new List<PlayerConfig>
        {
            new PlayerConfig { PlayerId = "p1", TeamId = Team.Team1, HeroId = 10001, SpawnIndex = 0 }
        };

        [LabelText("队伍2玩家")]
        public List<PlayerConfig> Team2Players = new List<PlayerConfig>
        {
            new PlayerConfig { PlayerId = "p2", TeamId = Team.Team2, HeroId = 10002, SpawnIndex = 0 }
        };
    }
}
