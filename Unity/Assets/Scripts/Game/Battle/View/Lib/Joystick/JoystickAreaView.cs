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

        private int _pointerId = int.MinValue;
        private Camera _uiCamera;

        private Vector2 _centerLocal;
        private JoystickOutput _output;

        public JoystickConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public JoystickOutput Output => _output;

        public event Action OnBegin;
        public event Action<JoystickOutput> OnValueChanged;
        public event Action OnEnd;

        private void Awake()
        {
            if (_config.Radius <= 0f) _config = JoystickConfig.Default;

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (_area == null)
            {
                _area = transform as RectTransform;
            }

            _uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            if (_outer != null && _config.HideWhenReleased) _outer.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue) return;
            if (_area == null) return;

            _pointerId = eventData.pointerId;
            _centerLocal = ScreenToLocalInRect(_area, eventData.position);

            if (_outer != null)
            {
                _outer.anchoredPosition = _centerLocal;
                if (_config.HideWhenReleased) _outer.gameObject.SetActive(true);
            }

            if (_inner != null)
            {
                _inner.anchoredPosition = _centerLocal;
            }

            UpdateOutput(eventData.position);
            OnBegin?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            UpdateOutput(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            _pointerId = int.MinValue;
            _output = default;

            if (_inner != null)
            {
                _inner.anchoredPosition = _centerLocal;
            }

            if (_outer != null && _config.HideWhenReleased)
            {
                _outer.gameObject.SetActive(false);
            }

            OnValueChanged?.Invoke(_output);
            OnEnd?.Invoke();
        }

        private void UpdateOutput(Vector2 screenPos)
        {
            if (_area == null) return;

            var local = ScreenToLocalInRect(_area, screenPos);
            var delta = local - _centerLocal;

            var dist = delta.magnitude;
            var dead = Mathf.Max(0f, _config.DeadZone);
            var radius = Mathf.Max(1f, _config.Radius);

            Vector2 clamped;
            float magnitude;

            if (dist <= dead)
            {
                clamped = Vector2.zero;
                magnitude = 0f;
            }
            else
            {
                var effective = Mathf.Min(dist, radius);
                clamped = delta * (effective / dist);
                magnitude = effective / radius;
            }

            if (_inner != null)
            {
                _inner.anchoredPosition = _centerLocal + clamped;
            }

            var value = clamped / radius;
            _output = new JoystickOutput(value, magnitude);
            OnValueChanged?.Invoke(_output);
        }

        private Vector2 ScreenToLocalInRect(RectTransform rect, Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, _uiCamera, out var local);
            return local;
        }
    }
}
