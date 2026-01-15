using UnityEditor;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugCopyEntityIdCommand : IBattleDebugToolbarCommand
    {
        public string Label => "Copy Id";
        public int Order => 200;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public bool IsEnabled(in BattleDebugContext ctx) => ctx.HasSelection;

        public void Execute(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection) return;
            EditorGUIUtility.systemCopyBuffer = ctx.SelectedId.ToString();
        }
    }
}
