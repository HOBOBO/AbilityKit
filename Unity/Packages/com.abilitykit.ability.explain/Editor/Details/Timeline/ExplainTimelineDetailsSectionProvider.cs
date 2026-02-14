using AbilityKit.Ability.Explain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainTimelineDetailsSectionProvider : IExplainDetailsSectionProvider
    {
        public int Priority => -100;

        public bool CanProvide(ExplainNode node, ExplainDetailsContext context)
        {
            if (node == null || context?.Result == null) return false;
            if (context.Result.Timeline == null || context.Result.Timeline.Events == null) return false;
            if (string.IsNullOrEmpty(node.NodeId)) return false;

            for (var i = 0; i < context.Result.Timeline.Events.Count; i++)
            {
                var e = context.Result.Timeline.Events[i];
                if (e != null && e.OwnerNodeId == node.NodeId) return true;
            }

            return false;
        }

        public void Build(VisualElement container, ExplainNode node, ExplainDetailsContext context)
        {
            var header = new Label("Timeline")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 10,
                    paddingLeft = AbilityExplainStyles.Padding
                }
            };

            container.Add(header);

            var events = context.Result.Timeline.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null) continue;
                if (e.OwnerNodeId != node.NodeId) continue;

                container.Add(BuildEventRow(e));
            }
        }

        private VisualElement BuildEventRow(ExplainTimelineEvent e)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = AbilityExplainStyles.Padding,
                    paddingRight = AbilityExplainStyles.Padding,
                    height = 20
                }
            };

            var badge = new VisualElement();
            AbilityExplainStyles.ApplySeverityBadge(badge, e.Severity);
            badge.style.visibility = e.Severity == ExplainSeverity.None ? Visibility.Hidden : Visibility.Visible;
            row.Add(badge);

            row.Add(new Label($"t={e.TimeSeconds:0.00}s") { style = { width = 70 } });
            row.Add(new Label(e.Title ?? string.Empty) { style = { flexGrow = 1 } });

            if (e.NavigateTo != null)
            {
                var btn = new Button(() =>
                {
                    var nav = AbilityExplainRegistry.GetNavigator(e.NavigateTo);
                    if (nav == null) return;
                    if (!nav.CanNavigate(e.NavigateTo)) return;
                    nav.Navigate(e.NavigateTo);
                })
                {
                    text = "Open"
                };

                btn.style.marginLeft = AbilityExplainStyles.Padding;
                row.Add(btn);
            }

            if (!string.IsNullOrEmpty(e.Title)) row.tooltip = e.Title;

            return row;
        }
    }
}
