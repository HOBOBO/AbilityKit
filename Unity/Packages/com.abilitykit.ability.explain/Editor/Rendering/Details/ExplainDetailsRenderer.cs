using System;
using AbilityKit.Ability.Explain;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainDetailsRenderer
    {
        private readonly ScrollView _detailsView;
        private readonly Action<ExplainAction> _onAction;

        public ExplainDetailsRenderer(ScrollView detailsView, Action<ExplainAction> onAction)
        {
            _detailsView = detailsView;
            _onAction = onAction;
        }

        public void Render(ExplainNode node, ExplainDetailsContext context)
        {
            if (_detailsView == null) return;

            _detailsView.Clear();

            var title = new Label(node.Title);
            AbilityExplainStyles.ApplyDetailsTitle(title);
            _detailsView.Add(title);

            if (node.SummaryLines != null)
            {
                foreach (var line in node.SummaryLines)
                {
                    var l = new Label(line);
                    AbilityExplainStyles.ApplyDetailsSummary(l);
                    _detailsView.Add(l);
                }
            }

            if (node.Actions != null && node.Actions.Count > 0)
            {
                var actionsRow = new VisualElement();
                AbilityExplainStyles.ApplyActionsRow(actionsRow);

                foreach (var a in node.Actions)
                {
                    var btn = new Button(() => _onAction?.Invoke(a)) { text = a.Name };
                    AbilityExplainStyles.ApplyActionButton(btn);
                    actionsRow.Add(btn);
                }

                _detailsView.Add(actionsRow);
            }

            if (node.Source != null)
            {
                var src = new Label($"Source: {node.Source.Kind} {node.Source.TableName}#{node.Source.RowId} {node.Source.FieldPath}");
                AbilityExplainStyles.ApplyDetailsSource(src);
                _detailsView.Add(src);
            }

            var providers = AbilityExplainRegistry.GetDetailsSectionProviders(node, context);
            if (providers == null || providers.Count <= 0) return;

            for (var i = 0; i < providers.Count; i++)
            {
                var p = providers[i];
                if (p == null) continue;
                p.Build(_detailsView, node, context);
            }
        }
    }
}
