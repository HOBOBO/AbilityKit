using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Protocol.Moba;
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
            [LabelText("鐜╁ID")]
            public string PlayerId;

            [LabelText("闃熶紞")]
            public Team TeamId = Team.Team1;

            [LabelText("涓讳綋绫诲瀷")]
            public EntityMainType MainType = EntityMainType.Unit;

            [LabelText("Unit Sub Type")]
            public UnitSubType UnitSubType = UnitSubType.Hero;

            [LabelText("鑻遍泟ID")]
            public int HeroId = 10001;

            [LabelText("灞炴€фā鏉縄D")]
            public int AttributeTemplateId = 0;

            [LabelText("绛夌骇")]
            public int Level = 1;

            [LabelText("鏅敾鎶€鑳絀D")]
            public int BasicAttackSkillId = 1;

            [LabelText("鎶€鑳絀D鍒楄〃")]
            public int[] SkillIds;

            [LabelText("Spawn Index")]
            public int SpawnIndex = 0;

            [LabelText("Spawn Position")]
            public Vector3 SpawnPosition = default;

            [LabelText("AI Brain Id (0 = Player Controlled)")]
            public int BrainId;

            [LabelText("Enable AI On Spawn")]
            public bool EnableBrainOnSpawn = true;
        }

        [LabelText("鏈湴鐜╁ID")]
        public string LocalPlayerId = "p1";

        [LabelText("闃熶紞1鐜╁")]
        public List<PlayerConfig> Team1Players = new List<PlayerConfig>
        {
            new PlayerConfig
            {
                PlayerId = "p1",
                TeamId = Team.Team1,
                HeroId = 3001,
                AttributeTemplateId = 3001,
                BasicAttackSkillId = 30010601,
                SkillIds = new[] { 30010101, 30010201, 30010301, 30010401 },
                SpawnIndex = 0
            }
        };

        [LabelText("闃熶紞2鐜╁")]
        public List<PlayerConfig> Team2Players = new List<PlayerConfig>
        {
            new PlayerConfig
            {
                PlayerId = "p2",
                TeamId = Team.Team2,
                HeroId = 3005,
                AttributeTemplateId = 3005,
                BasicAttackSkillId = 30050101,
                SkillIds = new[] { 30050201, 30050301, 30050401, 30050501 },
                SpawnIndex = 0,
                SpawnPosition = new Vector3(6f, 0f, 0f),
                BrainId = 100,
                EnableBrainOnSpawn = false
            }
        };
    }
}
