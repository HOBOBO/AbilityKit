using AbilityKit.Core.Common.AttributeSystem;
using UnityEditor;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugAttributesPanel : IBattleDebugPanel
    {
        public string Name => "属性";
        public int Order => 200;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox("请先选择一个实体。", MessageType.Info);
                return;
            }

            var unit = ctx.SelectedUnit;
            EditorGUILayout.LabelField("属性", EditorStyles.boldLabel);

            var attrCtx = unit.Attributes;
            if (attrCtx == null || attrCtx.Groups == null || attrCtx.Groups.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
                return;
            }

            foreach (var groupKv in attrCtx.Groups)
            {
                var groupName = string.IsNullOrEmpty(groupKv.Key) ? "默认" : groupKv.Key;
                var group = groupKv.Value;
                if (group == null) continue;

                EditorGUILayout.LabelField(groupName, EditorStyles.miniBoldLabel);

                if (group.Attributes == null || group.Attributes.Count == 0)
                {
                    EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
                    continue;
                }

                foreach (var attrKv in group.Attributes)
                {
                    var attrId = AttributeId.FromRaw(attrKv.Key);
                    var inst = attrKv.Value;
                    if (inst == null) continue;

                    var name = AttributeRegistry.Instance.GetName(attrId);
                    var v = inst.Value;
                    EditorGUILayout.LabelField($"{name} ({attrKv.Key})", v.ToString("0.#####"), EditorStyles.miniLabel);
                }
            }
        }
    }
}
