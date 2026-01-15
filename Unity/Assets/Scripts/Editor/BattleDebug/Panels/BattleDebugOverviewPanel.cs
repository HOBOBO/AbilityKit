using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugOverviewPanel : IBattleDebugPanel
    {
        public string Name => "Overview";
        public int Order => 0;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox("Select an entity.", MessageType.Info);
                return;
            }

            var unit = ctx.SelectedUnit;

            EditorGUILayout.LabelField("Entity", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Id", unit.Id.ToString());

            var tagCount = unit.Tags?.Count ?? 0;
            var effectCount = unit.Effects?.Active?.Count ?? 0;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tags", tagCount.ToString());
            EditorGUILayout.LabelField("Effects", effectCount.ToString());

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Id", GUILayout.Width(100)))
            {
                EditorGUIUtility.systemCopyBuffer = unit.Id.ToString();
            }

            if (GUILayout.Button("Copy Tags", GUILayout.Width(100)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildTagList(unit);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string BuildTagList(AbilityKit.Ability.Share.ECS.IUnitFacade unit)
        {
            if (unit == null || unit.Tags == null || unit.Tags.Count == 0) return string.Empty;

            var sb = new StringBuilder(256);
            foreach (var tag in unit.Tags)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(tag.ToString());
            }
            return sb.ToString();
        }
    }
}
