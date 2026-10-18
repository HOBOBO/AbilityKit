using System;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudRespawnClock : IDisposable
    {
        private GameObject _root;
        private Text _label;
        private GameplayRespawnData[] _pending = Array.Empty<GameplayRespawnData>();
        private int _localActorId;

        public void Bind(RectTransform hudRoot)
        {
            Dispose();
            if (hudRoot == null) return;
            _root = new GameObject("RespawnClock", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -150f);
            rect.sizeDelta = new Vector2(176f, 38f);
            var background = _root.GetComponent<Image>();
            background.color = new Color(0.15f, 0.08f, 0.08f, 0.88f);
            background.raycastTarget = false;

            var label = new GameObject("Remaining", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label = label.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 19;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _root.SetActive(false);
        }

        public void SetLocalActorId(int actorId)
        {
            if (_localActorId == actorId) return;
            _localActorId = actorId;
            Refresh();
        }

        public void Apply(GameplayRespawnData[] pending)
        {
            _pending = pending ?? Array.Empty<GameplayRespawnData>();
            Refresh();
        }

        private void Refresh()
        {
            if (_root == null) return;
            foreach (var entry in _pending)
            {
                if (entry.ActorId != _localActorId || _localActorId <= 0) continue;
                var milliseconds = Math.Max(0, entry.RemainingMilliseconds);
                var seconds = milliseconds / 1000 + (milliseconds % 1000 == 0 ? 0 : 1);
                _label.text = $"复活 {seconds} 秒";
                _root.SetActive(true);
                return;
            }
            _root.SetActive(false);
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
            _label = null;
            _pending = Array.Empty<GameplayRespawnData>();
            _localActorId = 0;
        }
    }
}
