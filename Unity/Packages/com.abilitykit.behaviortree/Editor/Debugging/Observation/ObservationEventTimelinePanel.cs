#nullable enable

using System.Collections.Generic;
using AbilityKit.BehaviorTree.Editor.Debugging.Contributors;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    /// <summary>
    /// 观察模式侧栏的结构化事件时间线（UI Toolkit 版）。从 <see cref="ObservationTimeline"/>
    /// 枚举扁平化事件（节点状态转换 + 黑板值变化），最新在前；支持文本过滤与清空，
    /// 取代旧 <c>DebugObservationWindow.DrawEventHistory</c> 的 IMGUI 实现。
    /// </summary>
    public sealed class ObservationEventTimelinePanel : VisualElement
    {
        private readonly ObservationController _controller;
        private readonly ObservationContributorRegistry _contributors;
        private readonly Label _title;
        private readonly ScrollView _scroll;
        private string _filter = "";

        public ObservationEventTimelinePanel(
            ObservationController controller,
            ObservationContributorRegistry contributors)
        {
            _controller = controller;
            _contributors = contributors;

            style.flexGrow = 0f;
            style.flexShrink = 0f;
            style.borderTopWidth = 1f;
            style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            style.paddingTop = 6f;

            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, height = 22f },
            };
            _title = new Label
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1f, whiteSpace = WhiteSpace.NoWrap },
            };
            header.Add(_title);

            var filterField = new TextField { value = _filter };
            filterField.style.flexGrow = 1f;
            filterField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? "";
                Refresh();
            });
            header.Add(filterField);

            var clearButton = new Button(ClearHistory) { text = "清空" };
            clearButton.style.height = 20f;
            header.Add(clearButton);
            Add(header);

            _scroll = new ScrollView { style = { flexGrow = 1f, height = 150f } };
            Add(_scroll);
        }

        /// <summary>按当前控制器时间线重建事件列表；由宿主在每个观察 tick 调用。</summary>
        public void Refresh()
        {
            var timeline = _controller.Timeline;
            _title.text = "结构化时间线 (" + timeline.Count + ")";

            _scroll.Clear();
            var changes = new List<ObservationChange>(timeline.EnumerateChanges());
            var shown = 0;
            for (var i = changes.Count - 1; i >= 0; i--)
            {
                var item = changes[i];
                if (_filter.Length > 0
                    && item.Target.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (_contributors.Filters.Count > 0
                    && !_contributors.AnyFilterMatches(ObservationFilterContext.ForChange(item)))
                {
                    continue;
                }

                _scroll.Add(new Label("f" + item.Frame + "  " + item.Kind + "  " + item.Target)
                {
                    tooltip = item.From + " -> " + item.To,
                    style = { whiteSpace = WhiteSpace.Normal, paddingTop = 1f, paddingBottom = 1f },
                });
                shown++;
            }

            if (shown == 0)
            {
                _scroll.Add(new Label("等待下一次采样差异") { style = { opacity = 0.65f } });
            }
        }

        private void ClearHistory()
        {
            _controller.ClearHistory();
            Refresh();
        }
    }
}
