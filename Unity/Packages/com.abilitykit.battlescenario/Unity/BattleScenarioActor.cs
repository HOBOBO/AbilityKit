using System;
using System.Linq;
using AbilityKit.BattleScenario;
using AbilityKit.Scenario;
using UnityEngine;

namespace AbilityKit.BattleScenario.Unity
{
    /// <summary>Spatial actor authoring component stored below a BattleScenarioRoot.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleScenarioActor : MonoBehaviour
    {
        [SerializeField] private string alias = "actor";
        [SerializeField] private string playerId = string.Empty;
        [SerializeField] private int teamId = 1;
        [SerializeField] private int heroId;
        [SerializeField] private int attributeTemplateId;
        [SerializeField] private int[] skillIds = Array.Empty<int>();

        public string Alias { get => alias; set => alias = value ?? string.Empty; }
        public string PlayerId { get => playerId; set => playerId = value ?? string.Empty; }
        public int TeamId { get => teamId; set => teamId = value; }
        public int HeroId { get => heroId; set => heroId = value; }
        public int AttributeTemplateId { get => attributeTemplateId; set => attributeTemplateId = value; }
        public int[] SkillIds { get => skillIds ?? Array.Empty<int>(); set => skillIds = value ?? Array.Empty<int>(); }

        public SpawnActorBlock ToBlock(Transform root)
        {
            var local = root.InverseTransformPoint(transform.position);
            return new SpawnActorBlock
            {
                Alias = alias?.Trim() ?? string.Empty,
                PlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim(),
                TeamId = teamId,
                HeroId = heroId,
                AttributeTemplateId = attributeTemplateId,
                SkillIds = SkillIds.ToArray(),
                Position = new TestVector3(local.x, local.y, local.z),
            };
        }

        private void OnValidate()
        {
            alias = alias?.Trim() ?? string.Empty;
            playerId = playerId?.Trim() ?? string.Empty;
            skillIds ??= Array.Empty<int>();
        }
    }
}
