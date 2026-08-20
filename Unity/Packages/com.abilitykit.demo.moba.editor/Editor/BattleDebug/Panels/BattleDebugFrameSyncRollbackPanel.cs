using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(
        BattleDebugModuleIds.FrameSyncRollback,
        "Frame Sync",
        Sources = BattleDebugModuleSourceSupport.All,
        Selections = BattleDebugModuleSelectionSupport.Frame)]
    internal sealed class BattleDebugFrameSyncRollbackPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "帧同步/回滚";
        public int Order => 52;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => false;

        public bool IsVisible(in BattleDebugContext ctx)
        {
            return BattleDebugFrameMetricHistory.IsAvailable(
                       in ctx,
                       BattleDiagnosticMetricCategory.Rollback) ||
                   !ctx.IsOffline &&
                   EditorApplication.isPlaying &&
                   BattleFlowDebugProvider.Current != null;
        }

        public void Draw(in BattleDebugContext ctx)
        {
            var hasHistory = BattleDebugFrameMetricHistory.Draw(
                in ctx,
                BattleDiagnosticMetricCategory.Rollback,
                "Rollback History");
            var flowCtx = ctx.IsOffline ? null : BattleFlowDebugProvider.Current;
            if (flowCtx == null)
            {
                if (hasHistory) return;
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current 为空。", MessageType.Info);
                return;
            }

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("PredictionStats 为空。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("是否正在回放", flowCtx.PredictionStats.IsReplaying.ToString());
            EditorGUILayout.LabelField("回放到帧", flowCtx.PredictionStats.ReplayToFrame.Value.ToString());
            EditorGUILayout.LabelField("最近回滚帧", flowCtx.PredictionStats.LastRollbackFrame.Value.ToString());
            EditorGUILayout.LabelField("回滚次数（总）", flowCtx.PredictionStats.TotalRollbackCount.ToString());
            EditorGUILayout.LabelField("回滚恢复失败次数（总）", flowCtx.PredictionStats.TotalRollbackRestoreFailed.ToString());
        }
    }
}
