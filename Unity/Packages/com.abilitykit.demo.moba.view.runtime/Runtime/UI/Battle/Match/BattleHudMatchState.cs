using System;
using System.Globalization;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudMatchState : IDisposable
    {
        private GameObject _root;
        private Text _score;
        private GameObject _resultRoot;
        private Text _result;

        public void Bind(RectTransform hudRoot)
        {
            Dispose();
            if (hudRoot == null) return;

            _root = new GameObject("MatchState", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -68f);
            rect.sizeDelta = new Vector2(200f, 76f);

            _score = CreateLabel(rect, "Score", 0f, 34f, 22);
            _resultRoot = new GameObject("Result", typeof(RectTransform));
            var resultRect = (RectTransform)_resultRoot.transform;
            resultRect.SetParent(rect, false);
            resultRect.anchorMin = new Vector2(0f, 1f);
            resultRect.anchorMax = new Vector2(1f, 1f);
            resultRect.pivot = new Vector2(0.5f, 1f);
            resultRect.anchoredPosition = new Vector2(0f, -40f);
            resultRect.sizeDelta = new Vector2(0f, 36f);
            _result = CreateLabel(resultRect, "Outcome", 0f, 36f, 18);
            _resultRoot.SetActive(false);
            _root.SetActive(false);
        }

        public void Apply(in GameplayMatchData state)
        {
            if (_root == null) return;
            var visible = state.GameplayId > 0 &&
                (state.Phase == GameplayClockPhase.Running || state.Phase == GameplayClockPhase.Ended);
            _root.SetActive(visible);
            if (!visible) return;

            _score.text = FormatScore(state.Team1Score) + " : " + FormatScore(state.Team2Score);
            var ended = state.Phase == GameplayClockPhase.Ended;
            _resultRoot.SetActive(ended);
            if (ended)
                _result.text = state.WinTeamId == 0 ? "平局" : "队伍 " + state.WinTeamId + " 获胜";
        }

        private static string FormatScore(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static Text CreateLabel(RectTransform parent, string name, float y, float height, int fontSize)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, height);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
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
            _score = null;
            _resultRoot = null;
            _result = null;
        }
    }
}
