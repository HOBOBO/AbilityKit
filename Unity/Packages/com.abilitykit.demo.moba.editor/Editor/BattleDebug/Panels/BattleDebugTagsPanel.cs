using UnityEditor;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugTagsPanel : IBattleDebugPanel
    {
        public string Name => "Tags";
        public int Order => 100;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox("Select an entity.", MessageType.Info);
                return;
            }

            var unit = ctx.SelectedUnit;
            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);

            if (unit.Tags == null || unit.Tags.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                return;
            }

            foreach (var tag in unit.Tags)
            {
                EditorGUILayout.LabelField(tag.ToString(), EditorStyles.miniLabel);
            }
        }
    }
}
