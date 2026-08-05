using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudCanvasProjector
    {
        private readonly RectTransform _root;
        private readonly Camera _camera;

        public BattleHudCanvasProjector(RectTransform root, Camera camera)
        {
            _root = root;
            _camera = camera;
        }

        public bool TryProject(Vector3 worldPos, out Vector2 local)
        {
            local = default;
            if (_root == null || _camera == null) return false;

            var canvas = _root.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            var screen = _camera.WorldToScreenPoint(worldPos);
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _camera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, uiCamera, out local);
        }
    }
}
