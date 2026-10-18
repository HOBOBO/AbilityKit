using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Joystick
{
    internal sealed class JoystickVisualController
    {
        private RectTransform _outer;
        private RectTransform _inner;
        private JoystickConfig _config;

        public void Configure(RectTransform outer, RectTransform inner, JoystickConfig config)
        {
            _outer = outer;
            _inner = inner;
            _config = config;
        }

        public void ApplyReleased(Vector2 centerLocal)
        {
            if (_inner != null)
            {
                _inner.anchoredPosition = centerLocal;
            }

            if (_outer != null && _config.HideWhenReleased)
            {
                _outer.gameObject.SetActive(false);
            }
        }

        public void ApplyReady()
        {
            if (_outer != null)
            {
                _outer.gameObject.SetActive(!_config.HideWhenReleased);
            }
        }

        public void ApplyPressed(Vector2 centerLocal)
        {
            if (_outer != null)
            {
                _outer.anchoredPosition = centerLocal;
                if (_config.HideWhenReleased) _outer.gameObject.SetActive(true);
            }

            if (_inner != null)
            {
                _inner.anchoredPosition = centerLocal;
            }
        }

        public void ApplyDrag(Vector2 centerLocal, Vector2 clamped)
        {
            if (_inner != null)
            {
                _inner.anchoredPosition = centerLocal + clamped;
            }
        }
    }
}
