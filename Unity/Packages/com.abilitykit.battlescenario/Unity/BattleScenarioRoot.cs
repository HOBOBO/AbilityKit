using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BattleScenario;
using AbilityKit.Scenario;
using UnityEngine;

namespace AbilityKit.BattleScenario.Unity
{
    /// <summary>Scene authoring root. Child components are converted to BattleSceneDocument.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleScenarioRoot : MonoBehaviour
    {
        [SerializeField] private string sceneId = "battle-scene";
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string environmentProfileId = string.Empty;

        public string SceneId { get => sceneId; set => sceneId = value ?? string.Empty; }
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public string EnvironmentProfileId { get => environmentProfileId; set => environmentProfileId = value ?? string.Empty; }

        public BattleSceneDocument ToSceneDocument()
        {
            var scene = new BattleSceneDocument
            {
                SceneId = string.IsNullOrWhiteSpace(sceneId) ? gameObject.name : sceneId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName,
            };
            if (!string.IsNullOrWhiteSpace(environmentProfileId))
                scene.Sections.Add(new SetEnvironmentBlock { ProfileId = environmentProfileId });
            foreach (var actor in GetComponentsInChildren<BattleScenarioActor>(true))
                if (actor.transform != transform) scene.Sections.Add(actor.ToBlock(transform));
            foreach (var obstacle in GetComponentsInChildren<BattleScenarioObstacle>(true))
                if (obstacle.transform != transform) scene.Sections.Add(obstacle.ToBlock(transform));
            return scene;
        }

        public IReadOnlyList<string> ValidateAuthoring()
        {
            var errors = new List<string>();
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var obstacleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var actor in GetComponentsInChildren<BattleScenarioActor>(true))
            {
                if (actor.transform == transform) continue;
                var alias = (actor.Alias ?? string.Empty).Trim();
                if (alias.Length == 0) errors.Add($"Actor '{actor.name}' has an empty alias.");
                else if (!aliases.Add(alias)) errors.Add($"Actor alias '{alias}' is duplicated.");
            }
            foreach (var obstacle in GetComponentsInChildren<BattleScenarioObstacle>(true))
            {
                if (obstacle.transform == transform) continue;
                var id = (obstacle.ObstacleId ?? string.Empty).Trim();
                if (id.Length == 0) errors.Add($"Obstacle '{obstacle.name}' has an empty id.");
                else if (!obstacleIds.Add(id)) errors.Add($"Obstacle id '{id}' is duplicated.");
            }
            errors.AddRange(BattleScenarioDocumentValidator.Validate(ToSceneDocument()));
            return errors.Distinct(StringComparer.Ordinal).ToArray();
        }

        private void OnValidate()
        {
            sceneId = sceneId?.Trim() ?? string.Empty;
            displayName = displayName ?? string.Empty;
            environmentProfileId = environmentProfileId?.Trim() ?? string.Empty;
        }
    }

}
