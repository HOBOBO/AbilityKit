using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityKit.Game.Battle.View.Lib.Joystick
{
    public sealed class JoystickAreaView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _area;
        [SerializeField] private RectTransform _outer;
        [SerializeField] private RectTransform _inner;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private JoystickConfig _config = default;

        private readonly JoystickPointerSession _session = new JoystickPointerSession();
        private readonly JoystickVisualController _visual = new JoystickVisualController();
        private Camera _uiCamera;

        public JoystickConfig Config
        {
            get => _config;
            set
            {
                _config = value.Radius > 0f ? value : JoystickConfig.Default;
                ConfigureVisual();
            }
        }

        public JoystickOutput Output => _session.Output;

        public event Action OnBegin;
        public event Action<JoystickOutput> OnValueChanged;
        public event Action OnEnd;

        public void Initialize(RectTransform area, RectTransform outer, RectTransform inner, Canvas canvas, JoystickConfig config)
        {
            _area = area;
            _outer = outer;
            _inner = inner;
            _canvas = canvas;
            _config = config.Radius > 0f ? config : JoystickConfig.Default;

            RefreshReferences();
            _visual.ApplyReady();
        }

        private void Awake()
        {
            if (_config.Radius <= 0f) _config = JoystickConfig.Default;
            RefreshReferences();
            _visual.ApplyReleased(_session.CenterLocal);
        }

        private void RefreshReferences()
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (_area == null)
            {
                _area = transform as RectTransform;
            }

            _uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            ConfigureVisual();
        }

        private void ConfigureVisual()
        {
            _visual.Configure(_outer, _inner, _config);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_area == null) return;

            var centerLocal = ScreenToLocalInRect(_area, eventData.position);
            if (!_session.Begin(eventData.pointerId, centerLocal)) return;

            _visual.ApplyPressed(centerLocal);

            UpdateOutput(eventData.pointerId, eventData.position);
            OnBegin?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateOutput(eventData.pointerId, eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_session.End(eventData.pointerId, out var output)) return;

            _visual.ApplyReleased(_session.CenterLocal);

            OnValueChanged?.Invoke(output);
            OnEnd?.Invoke();
        }

        private void UpdateOutput(int pointerId, Vector2 screenPos)
        {
            if (_area == null) return;

            var local = ScreenToLocalInRect(_area, screenPos);
            if (!_session.Update(pointerId, local, _config, out var output, out var clamped)) return;

            _visual.ApplyDrag(_session.CenterLocal, clamped);

            OnValueChanged?.Invoke(output);
        }

        private Vector2 ScreenToLocalInRect(RectTransform rect, Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, _uiCamera, out var local);
            return local;
        }
    }
}
