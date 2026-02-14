using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal static class AbilityExplainStyles
    {
        public const int Padding = 6;
        public const int ToolbarHeight = 28;
        public const int EntityRowHeight = 20;
        public const int DiscoveryRowHeight = 22;

        private static readonly Color DiffAddedColor = new Color(0.2f, 0.85f, 0.45f, 0.16f);
        private static readonly Color DiffRemovedColor = new Color(0.95f, 0.35f, 0.35f, 0.16f);
        private static readonly Color DiffChangedColor = new Color(0.95f, 0.75f, 0.25f, 0.16f);

        public static Color GetSeverityColor(ExplainSeverity severity)
        {
            switch (severity)
            {
                case ExplainSeverity.Error:
                    return new Color(0.85f, 0.25f, 0.25f, 1f);
                case ExplainSeverity.Warning:
                    return new Color(0.95f, 0.65f, 0.15f, 1f);
                case ExplainSeverity.Info:
                    return new Color(0.25f, 0.55f, 0.95f, 1f);
                default:
                    return new Color(0f, 0f, 0f, 0f);
            }
        }

        public static void ApplySeverityBadge(VisualElement badge, ExplainSeverity severity)
        {
            badge.style.width = 8;
            badge.style.height = 8;
            badge.style.marginRight = 6;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.backgroundColor = GetSeverityColor(severity);

            if (severity == ExplainSeverity.None)
            {
                badge.style.visibility = Visibility.Hidden;
            }
        }

        public static void ApplyToolbar(VisualElement toolbar)
        {
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = ToolbarHeight;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = Padding;
            toolbar.style.paddingRight = Padding;
            toolbar.style.borderBottomWidth = 1;
        }

        public static void ApplyTreeHeader(Label label)
        {
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            label.style.paddingLeft = Padding;
        }

        public static void ApplyMissingHint(Label label)
        {
            label.style.paddingLeft = Padding;
            label.style.paddingTop = Padding;
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
        }

        public static void ApplyNodeRow(VisualElement row, int indent)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = Padding + indent * 16;
            row.style.paddingRight = Padding;
            row.style.height = EntityRowHeight;
        }

        public static void ApplyDiffBadge(Label label, ExplainDiffKind kind)
        {
            label.style.width = 14;
            label.style.marginRight = 6;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = kind == ExplainDiffKind.None ? 0f : 1f;
        }

        public static void ApplyDiffRowBackground(VisualElement row, ExplainDiffKind kind)
        {
            switch (kind)
            {
                case ExplainDiffKind.Added:
                    row.style.backgroundColor = DiffAddedColor;
                    break;
                case ExplainDiffKind.Removed:
                    row.style.backgroundColor = DiffRemovedColor;
                    break;
                case ExplainDiffKind.Changed:
                    row.style.backgroundColor = DiffChangedColor;
                    break;
            }
        }

        public static void ApplyDiscoveryRow(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = Padding;
            row.style.paddingRight = Padding;
            row.style.height = DiscoveryRowHeight;
        }

        public static void ApplyDetailsTitle(Label label)
        {
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = Padding;
            label.style.paddingLeft = Padding;
            label.style.paddingTop = Padding;
        }

        public static void ApplyDetailsSummary(Label label)
        {
            label.style.paddingLeft = Padding;
        }

        public static void ApplyDetailsSource(Label label)
        {
            label.style.paddingLeft = Padding;
            label.style.paddingTop = 8;
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
        }

        public static void ApplyActionsRow(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.paddingLeft = Padding;
            row.style.paddingTop = 8;
        }

        public static void ApplyActionButton(Button btn)
        {
            btn.style.marginRight = Padding;
            btn.style.marginBottom = Padding;
        }
    }
}
