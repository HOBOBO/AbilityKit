using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    internal sealed class BattleHudCameraTransformResolver
    {
        public Transform Resolve(Transform current)
        {
            if (current != null) return current;

            var mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : null;
        }
    }
}
