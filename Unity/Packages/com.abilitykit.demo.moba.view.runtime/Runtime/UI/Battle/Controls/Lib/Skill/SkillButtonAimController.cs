using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    internal sealed class SkillButtonAimController
    {
        private RectTransform _buttonRect;
        private RectTransform _uiRootRect;
        private Camera _uiCamera;
        private SkillAimIndicatorView _indicator;
        private SkillButtonConfig _config;

        public void Configure(
            RectTransform buttonRect,
            RectTransform uiRootRect,
            Camera uiCamera,
            SkillAimIndicatorView indicator,
            SkillButtonConfig config)
        {
            _buttonRect = buttonRect;
            _uiRootRect = uiRootRect;
            _uiCamera = uiCamera;
            _indicator = indicator;
            _config = config;
        }

        public Vector2 Calculate(Vector2 screenPos)
        {
            return SkillButtonAimCalculator.CalculateAim(
                _uiRootRect,
                _buttonRect,
                _uiCamera,
                _config,
                screenPos);
        }

        public void Show(Vector2 screenPos)
        {
            if (_indicator != null)
            {
                _indicator.SetVisible(true);
            }

            Update(screenPos);
        }

        public void Update(Vector2 screenPos)
        {
            if (_indicator == null || _uiRootRect == null || _buttonRect == null) return;

            if (SkillButtonAimCalculator.TryGetIndicatorPoints(
                    _uiRootRect,
                    _buttonRect,
                    _uiCamera,
                    _config,
                    screenPos,
                    out var from,
                    out var to,
                    out var radius))
            {
                _indicator.SetFromTo(from, to, maxRadius: radius, _config);
            }
        }

        public void Hide()
        {
            if (_indicator != null)
            {
                _indicator.SetVisible(false);
            }
        }
    }
}
