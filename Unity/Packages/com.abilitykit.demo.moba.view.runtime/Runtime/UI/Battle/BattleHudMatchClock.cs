using System;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudMatchClock : IDisposable
    {
        private GameObject _root;
        private Text _time;
        private int _shownSeconds = -1;

        public void Bind(RectTransform hudRoot)
        {
            Dispose();
            if (hudRoot == null) return;

            _root = new GameObject("MatchClock", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(112f, 40f);

            var background = _root.GetComponent<Image>();
            background.color = new Color(0.08f, 0.11f, 0.14f, 0.88f);
            background.raycastTarget = false;

            var label = new GameObject("Time", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _time = label.GetComponent<Text>();
            _time.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _time.fontSize = 25;
            _time.fontStyle = FontStyle.Bold;
            _time.alignment = TextAnchor.MiddleCenter;
            _time.color = Color.white;
            _time.raycastTarget = false;
            _root.SetActive(false);
        }

        public void Apply(in GameplayClockData clock)
        {
            if (_root == null) return;
            var visible = clock.IsVisible;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible)
            {
                _shownSeconds = -1;
                return;
            }

            var remaining = Math.Max(0, clock.RemainingMilliseconds);
            var seconds = remaining / 1000 + (remaining % 1000 == 0 ? 0 : 1);
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            _time.text = FormatTime(seconds);
        }

        internal static string FormatTime(int remainingSeconds)
        {
            var seconds = Math.Max(0, remainingSeconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        public void Dispose()
        {
            if (_root != null)
            {
                _root.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }
            _root = null;
            _time = null;
            _shownSeconds = -1;
        }
    }
}
