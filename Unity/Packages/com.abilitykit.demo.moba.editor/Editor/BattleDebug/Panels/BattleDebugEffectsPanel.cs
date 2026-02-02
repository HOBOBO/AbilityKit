using UnityEditor;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugEffectsPanel : IBattleDebugPanel
    {
        public string Name => "效果";
        public int Order => 300;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox("请先选择一个实体。", MessageType.Info);
                return;
            }

            var unit = ctx.SelectedUnit;
            EditorGUILayout.LabelField("效果", EditorStyles.boldLabel);

            var effects = unit.Effects;
            if (effects == null || effects.Active == null || effects.Active.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < effects.Active.Count; i++)
            {
                var inst = effects.Active[i];
                if (inst == null) continue;

                var spec = inst.Spec;
                var durationPolicy = spec != null ? spec.DurationPolicy.ToString() : string.Empty;
                EditorGUILayout.LabelField($"#{inst.Id} stack={inst.StackCount} duration={durationPolicy}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"elapsed={inst.ElapsedSeconds:0.###} remaining={inst.RemainingSeconds:0.###} nextTick={inst.NextTickInSeconds:0.###}", EditorStyles.miniLabel);
            }
        }
    }
}
