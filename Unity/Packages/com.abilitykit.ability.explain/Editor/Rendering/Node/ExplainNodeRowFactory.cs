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

        public VisualElement Create(ExplainNode node, int indent, ExplainDiffKind diffKind)
        {
            var row = new VisualElement();
            AbilityExplainStyles.ApplyNodeRow(row, indent);

            AbilityExplainStyles.ApplyDiffRowBackground(row, diffKind);

            var diff = new Label(DiffToText(diffKind));
            AbilityExplainStyles.ApplyDiffBadge(diff, diffKind);
            row.Add(diff);

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

        private static string DiffToText(ExplainDiffKind kind)
        {
            switch (kind)
            {
                case ExplainDiffKind.Added:
                    return "+";
                case ExplainDiffKind.Removed:
                    return "-";
                case ExplainDiffKind.Changed:
                    return "~";
                default:
                    return string.Empty;
            }
        }
    }
}
