using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncRollbackPanel : IBattleDebugPanel
    {
        public string Name => "FrameSync/Rollback";
        public int Order => 52;

        public bool IsVisible(in BattleDebugContext ctx)
        {
            return EditorApplication.isPlaying && BattleFlowDebugProvider.Current != null;
        }

        public void Draw(in BattleDebugContext ctx)
        {
            var flowCtx = BattleFlowDebugProvider.Current;
            if (flowCtx == null)
            {
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current is null.", MessageType.Info);
                return;
            }

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("PredictionStats is null.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Replaying", flowCtx.PredictionStats.IsReplaying.ToString());
            EditorGUILayout.LabelField("ReplayToFrame", flowCtx.PredictionStats.ReplayToFrame.Value.ToString());
            EditorGUILayout.LabelField("LastRollbackFrame", flowCtx.PredictionStats.LastRollbackFrame.Value.ToString());
            EditorGUILayout.LabelField("TotalRollbackCount", flowCtx.PredictionStats.TotalRollbackCount.ToString());
            EditorGUILayout.LabelField("TotalRestoreFailed", flowCtx.PredictionStats.TotalRollbackRestoreFailed.ToString());
        }
    }
}
