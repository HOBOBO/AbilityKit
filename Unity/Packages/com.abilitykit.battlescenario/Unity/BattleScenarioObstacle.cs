using AbilityKit.BattleScenario;
using AbilityKit.Scenario;
using UnityEngine;

namespace AbilityKit.BattleScenario.Unity
{
    /// <summary>Spatial obstacle authoring component stored below a BattleScenarioRoot.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleScenarioObstacle : MonoBehaviour
    {
        [SerializeField] private string obstacleId = "obstacle";
        [SerializeField] private string shape = "box";
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField] private string collisionProfileId = "default";

        public string ObstacleId { get => obstacleId; set => obstacleId = value ?? string.Empty; }
        public string Shape { get => shape; set => shape = value ?? "box"; }
        public Vector3 Size { get => size; set => size = value; }
        public string CollisionProfileId { get => collisionProfileId; set => collisionProfileId = value ?? "default"; }

        public PlaceObstacleBlock ToBlock(Transform root)
        {
            var local = root.InverseTransformPoint(transform.position);
            return new PlaceObstacleBlock
            {
                Id = obstacleId?.Trim() ?? string.Empty,
                Shape = string.IsNullOrWhiteSpace(shape) ? "box" : shape.Trim(),
                Size = new TestVector3(size.x, size.y, size.z),
                Position = new TestVector3(local.x, local.y, local.z),
                CollisionProfileId = string.IsNullOrWhiteSpace(collisionProfileId) ? "default" : collisionProfileId.Trim(),
            };
        }

        private void OnValidate()
        {
            obstacleId = obstacleId?.Trim() ?? string.Empty;
            shape = string.IsNullOrWhiteSpace(shape) ? "box" : shape.Trim();
            collisionProfileId = string.IsNullOrWhiteSpace(collisionProfileId) ? "default" : collisionProfileId.Trim();
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
            size.z = Mathf.Max(0.01f, size.z);
        }
    }
}
