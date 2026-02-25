using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainIssuesRenderer
    {
        private readonly ScrollView _issuesView;
        private readonly Action<ExplainIssue> _onIssue;

        public ExplainIssuesRenderer(ScrollView issuesView, Action<ExplainIssue> onIssue)
        {
            _issuesView = issuesView;
            _onIssue = onIssue;
        }

        public void Clear()
        {
            _issuesView?.Clear();
        }

        public void Render(List<ExplainIssue> issues)
        {
            if (_issuesView == null) return;

            _issuesView.Clear();

            var header = new Label("问题")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = AbilityExplainStyles.Padding,
                    paddingTop = 8,
                    paddingBottom = 4
                }
            };

            _issuesView.Add(header);

            if (issues == null || issues.Count <= 0)
            {
                var empty = new Label("（无）")
                {
                    style =
                    {
                        paddingLeft = AbilityExplainStyles.Padding,
                        unityFontStyleAndWeight = FontStyle.Italic
                    }
                };

                _issuesView.Add(empty);
                return;
            }

            foreach (var issue in issues)
            {
                _issuesView.Add(BuildRow(issue));
            }
        }

        private VisualElement BuildRow(ExplainIssue issue)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = AbilityExplainStyles.Padding,
                    paddingRight = AbilityExplainStyles.Padding,
                    height = 22
                }
            };

            var badge = new VisualElement();
            AbilityExplainStyles.ApplySeverityBadge(badge, issue.Severity);
            badge.style.visibility = Visibility.Visible;
            row.Add(badge);

            var title = new Label(issue.Title ?? string.Empty) { style = { flexGrow = 1 } };
            row.Add(title);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _onIssue?.Invoke(issue);
            });

            if (!string.IsNullOrEmpty(issue.Message))
            {
                row.tooltip = issue.Message;
            }

            return row;
        }
    }
}
