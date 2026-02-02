using UnityEditor;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugTagsPanel : IBattleDebugPanel
    {
        public string Name => "标签";
        public int Order => 100;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox("请先选择一个实体。", MessageType.Info);
                return;
            }

            var unit = ctx.SelectedUnit;
            EditorGUILayout.LabelField("标签", EditorStyles.boldLabel);

            if (unit.Tags == null || unit.Tags.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
                return;
            }

            foreach (var tag in unit.Tags)
            {
                EditorGUILayout.LabelField(tag.ToString(), EditorStyles.miniLabel);
            }
        }
    }
}
