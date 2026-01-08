using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    public sealed class SkillButtonView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private RectTransform _buttonRect;
        [SerializeField] private RectTransform _uiRootRect;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private SkillAimIndicatorView _aimIndicator;
        [SerializeField] private SkillButtonConfig _config = default;

        private int _pointerId = int.MinValue;
        private Camera _uiCamera;

        private bool _pressed;
        private bool _longPressFired;
        private bool _aiming;
        private float _pressTime;

        private Vector2 _pressScreenPos;
        private Vector2 _lastScreenPos;

        public SkillButtonConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public event Action OnClick;
        public event Action OnLongPress;
        public event Action<Vector2> OnAimStart;
        public event Action<Vector2> OnAimUpdate;
        public event Action<Vector2> OnAimEnd;

        private void Awake()
        {
            if (_config.LongPressSeconds <= 0f) _config = SkillButtonConfig.Default;

            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_buttonRect == null) _buttonRect = transform as RectTransform;
            if (_uiRootRect == null) _uiRootRect = _buttonRect != null ? _buttonRect.root as RectTransform : null;

            _uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            if (_aimIndicator != null) _aimIndicator.SetVisible(false);
        }

        private void Update()
        {
            if (!_pressed) return;
            if (_longPressFired) return;

            var now = Time.unscaledTime;
            if (now - _pressTime >= _config.LongPressSeconds)
            {
                _longPressFired = true;
                OnLongPress?.Invoke();

                if (_config.EnableAim)
                {
                    BeginAim(_lastScreenPos);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue) return;
            if (_buttonRect == null) return;

            _pointerId = eventData.pointerId;
            _pressed = true;
            _longPressFired = false;
            _aiming = false;
            _pressTime = Time.unscaledTime;

            _pressScreenPos = eventData.position;
            _lastScreenPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pressed) return;
            if (eventData.pointerId != _pointerId) return;

            _lastScreenPos = eventData.position;

            if (_config.EnableAim && !_aiming)
            {
                var drag = (_lastScreenPos - _pressScreenPos).magnitude;
                if (drag >= _config.DragThreshold)
                {
                    BeginAim(_lastScreenPos);
                }
            }

            if (_aiming)
            {
                var dir = CalcAimDir(_lastScreenPos);
                OnAimUpdate?.Invoke(dir);
                UpdateIndicator(_lastScreenPos);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            var wasAiming = _aiming;

            _pressed = false;
            _pointerId = int.MinValue;

            if (wasAiming)
            {
                var dir = CalcAimDir(eventData.position);
                OnAimEnd?.Invoke(dir);
                EndAim();
                return;
            }

            if (!_longPressFired)
            {
                OnClick?.Invoke();
            }

            EndAim();
        }

        private void BeginAim(Vector2 currentScreen)
        {
            _aiming = true;
            var dir = CalcAimDir(currentScreen);
            OnAimStart?.Invoke(dir);
            if (_aimIndicator != null) _aimIndicator.SetVisible(true);
            UpdateIndicator(currentScreen);
        }

        private void EndAim()
        {
            _aiming = false;
            if (_aimIndicator != null) _aimIndicator.SetVisible(false);
        }

        private Vector2 CalcAimDir(Vector2 screenPos)
        {
            if (_uiRootRect == null || _buttonRect == null) return Vector2.zero;

            var from = ScreenToLocalInRect(_uiRootRect, RectTransformUtility.WorldToScreenPoint(_uiCamera, _buttonRect.position));
            var to = ScreenToLocalInRect(_uiRootRect, screenPos);
            var delta = to - from;
            var dist = delta.magnitude;
            if (dist <= 0.0001f) return Vector2.zero;
            return delta / dist;
        }

        private void UpdateIndicator(Vector2 screenPos)
        {
            if (_aimIndicator == null || _uiRootRect == null || _buttonRect == null) return;

            var from = ScreenToLocalInRect(_uiRootRect, RectTransformUtility.WorldToScreenPoint(_uiCamera, _buttonRect.position));
            var to = ScreenToLocalInRect(_uiRootRect, screenPos);
            _aimIndicator.SetFromTo(from, to, maxRadius: 180f);
        }

        private Vector2 ScreenToLocalInRect(RectTransform rect, Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, _uiCamera, out var local);
            return local;
        }
    }
}
