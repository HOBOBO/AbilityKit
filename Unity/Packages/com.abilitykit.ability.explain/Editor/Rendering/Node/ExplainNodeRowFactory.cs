using System;
using AbilityKit.Ability.Explain;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainNodeRowFactory
    {
        private readonly Action<ExplainNode, bool> _onInvoked;
        private readonly Action<ExplainNode, DropdownMenu> _onContextMenuPopulate;

        public ExplainNodeRowFactory(Action<ExplainNode, bool> onInvoked, Action<ExplainNode, DropdownMenu> onContextMenuPopulate)
        {
            _onInvoked = onInvoked;
            _onContextMenuPopulate = onContextMenuPopulate;
        }

        public VisualElement Create(ExplainNode node, int indent)
        {
            var row = new VisualElement();
            AbilityExplainStyles.ApplyNodeRow(row, indent);

            var badge = new VisualElement();
            AbilityExplainStyles.ApplySeverityBadge(badge, node.Severity);
            row.Add(badge);

            var label = new Label(node.Title) { style = { flexGrow = 1 } };
            row.Add(label);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _onInvoked?.Invoke(node, evt.clickCount >= 2);
            });

            if (_onContextMenuPopulate != null)
            {
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    _onContextMenuPopulate?.Invoke(node, evt.menu);
                }));
            }

            return row;
        }
    }
}
