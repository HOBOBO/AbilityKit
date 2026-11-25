using System.Globalization;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 按渲染类型更新的持久指示器行；不识别英雄或读取玩法服务。
    /// </summary>
    internal sealed class BattleHudIndicatorRow
    {
        public readonly RectTransform Root;
        public readonly OverheadIndicatorRowDTO Definition;
        public bool Seen;
        private readonly Image _fill;
        private readonly Image[] _segments;
        private readonly Text _text;
        private readonly Color _color;
        private float _previous = float.NaN;
        private bool _previousReady;
        public BattleHudIndicatorRow(OverheadIndicatorRowDTO definition, float width, Transform parent, int segmentCount)
        {
            Definition = definition; _color = StyleColor(definition.Style);
            Root = BattleHudOverheadView.Rect("Indicator_" + definition.IndicatorId, parent, new Vector2(width, definition.Height));
            if (definition.Renderer == IndicatorRendererKind.ContinuousBar)
            {
                var bg = BattleHudOverheadView.Image("Background", Root, new Color(0.02f, 0.03f, 0.06f, 0.9f));
                bg.rectTransform.sizeDelta = Root.sizeDelta;
                _fill = BattleHudOverheadView.Image("Fill", bg.transform, _color);
            }
            else if (definition.Renderer == IndicatorRendererKind.Segments)
            {
                _segments = new Image[segmentCount];
                var widthPerCell = (width - 2 * (segmentCount - 1)) / segmentCount;
                for (var i = 0; i < segmentCount; i++)
                {
                    var bg = BattleHudOverheadView.Image("SegmentBackground_" + i, Root, new Color(0.05f, 0.06f, 0.08f, 1));
                    bg.rectTransform.sizeDelta = new Vector2(widthPerCell, definition.Height);
                    bg.rectTransform.anchoredPosition = new Vector2(-width / 2 + widthPerCell / 2 + i * (widthPerCell + 2), 0);
                    _segments[i] = BattleHudOverheadView.Image("Segment_" + i, bg.transform, _color);
                    BattleHudOverheadView.SetFill(_segments[i], 1);
                }
            }
            if (definition.Renderer != IndicatorRendererKind.ContinuousBar)
            {
                _text = BattleHudOverheadView.Text("Value", Root, 11);
                if (definition.Renderer == IndicatorRendererKind.Segments) _text.rectTransform.anchoredPosition = new Vector2(width / 2 + 21, 0);
            }
        }
        public void Apply(in ActorIndicatorStateData state, int localActorId)
        {
            Seen = true;
            var visible = state.Availability == 1 && (!Definition.LocalOnly || state.ActorId == localActorId);
            Root.gameObject.SetActive(visible); if (!visible) return;
            if (_fill != null) BattleHudOverheadView.SetFill(_fill, state.HasMax ? state.Current / state.Max : 0);
            if (_segments != null)
                for (var i = 0; i < _segments.Length; i++)
                { _segments[i].gameObject.SetActive(i < state.Current); _segments[i].color = state.Ready ? Color.white : _color; }
            if (_text != null && (_previous != state.Current || _previousReady != state.Ready))
            {
                _text.text = Definition.Renderer == IndicatorRendererKind.Number
                    ? Definition.Label + " " + state.Current.ToString("0", CultureInfo.InvariantCulture)
                    : state.Ready ? "◆" : state.Current == 0 && Definition.EmptyLabel != "-" ? Definition.EmptyLabel : "";
                _previous = state.Current; _previousReady = state.Ready;
            }
        }
        public static Color StyleColor(string key)
        {
            switch (key)
            {
                case "mana": return new Color(0.18f, 0.55f, 1);
                case "energy": return new Color(1, 0.85f, 0.15f);
                case "rage": return new Color(1, 0.24f, 0.12f);
                case "ammo": return new Color(1, 0.75f, 0.32f);
                case "counter": return new Color(0.75f, 0.4f, 1);
                default: return new Color(0.25f, 0.85f, 0.3f);
            }
        }
    }
}
