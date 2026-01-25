using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbilityKit.Game.UI
{
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private bool _createEventSystem = true;
        [SerializeField] private string _resourcesRoot = "UI/";

        private UIManager _ui;

        public UIManager UI => _ui;

        private void Awake()
        {
            EnsureCanvas();
            if (_createEventSystem) EnsureEventSystem();

            _ui = new UIManager(new ResourcesUIAssetProvider());

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var root = EnsureLayerRoot(layer);
                _ui.SetLayerRoot(layer, root);
            }
        }

        private void Update()
        {
            _ui?.Tick(Time.deltaTime);
        }

        public string Path(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return _resourcesRoot;
            return _resourcesRoot + relative;
        }

        private void EnsureCanvas()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        private Transform EnsureLayerRoot(UILayer layer)
        {
            var canvas = GetComponentInChildren<Canvas>();
            var layerName = layer.ToString();

            var existing = canvas.transform.Find(layerName);
            if (existing != null) return existing;

            var go = new GameObject(layerName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform, false);
        }
    }
}
