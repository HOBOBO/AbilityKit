using System;
using AbilityKit.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudCanvasController : IDisposable
    {
        private readonly BattleHudEventSystemController _eventSystem;
        private bool _ownsCanvas;

        public BattleHudCanvasController(BattleHudEventSystemController eventSystem = null)
        {
            _eventSystem = eventSystem ?? new BattleHudEventSystemController();
        }

        public Canvas Canvas { get; private set; }
        public RectTransform Root { get; private set; }

        public void Create(string name)
        {
            Destroy();

            var uiRoot = UnityEngine.Object.FindFirstObjectByType<UIRoot>();
            if (uiRoot != null &&
                uiRoot.TryGetLayerRoot(UILayer.Main, out var layerRoot) &&
                layerRoot is RectTransform sharedRoot)
            {
                Canvas = uiRoot.Canvas;
                Root = sharedRoot;
                _ownsCanvas = false;
                _eventSystem.Ensure();
                return;
            }

            var go = new GameObject(name);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            Root = Canvas.GetComponent<RectTransform>();
            _ownsCanvas = true;

            _eventSystem.Ensure();
        }

        public void Dispose()
        {
            Destroy();
        }

        private void Destroy()
        {
            if (_ownsCanvas && Canvas != null)
            {
                UnityEngine.Object.Destroy(Canvas.gameObject);
            }

            Canvas = null;
            Root = null;
            _ownsCanvas = false;
        }

    }

    internal sealed class BattleHudEventSystemController
    {
        public void Ensure()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.DontSave;
        }
    }
}
