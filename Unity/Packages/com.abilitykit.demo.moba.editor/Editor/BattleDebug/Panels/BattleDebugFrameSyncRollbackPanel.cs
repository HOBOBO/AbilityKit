using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(
        BattleDebugModuleIds.FrameSyncRollback,
        "帧同步",
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
                   BattleDebugFrameSyncContextResolver.TryResolve(in ctx, out _);
        }

        public void Draw(in BattleDebugContext ctx)
        {
            var hasHistory = BattleDebugFrameMetricHistory.Draw(
                in ctx,
                BattleDiagnosticMetricCategory.Rollback,
                "回滚历史");
            var flowCtx = BattleDebugFrameSyncContextResolver.TryResolve(in ctx, out var resolved)
                ? resolved : null;
            if (flowCtx == null)
            {
                if (hasHistory) return;
                EditorGUILayout.HelpBox("战斗流程调试数据源为空。", MessageType.Info);
                return;
            }

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("预测统计为空。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("是否正在回放", BattleDebugDisplayText.Bool(flowCtx.PredictionStats.IsReplaying));
            EditorGUILayout.LabelField("回放到帧", flowCtx.PredictionStats.ReplayToFrame.Value.ToString());
            EditorGUILayout.LabelField("最近回滚帧", flowCtx.PredictionStats.LastRollbackFrame.Value.ToString());
            EditorGUILayout.LabelField("回滚次数（总）", flowCtx.PredictionStats.TotalRollbackCount.ToString());
            EditorGUILayout.LabelField("回滚恢复失败次数（总）", flowCtx.PredictionStats.TotalRollbackRestoreFailed.ToString());
        }
    }
}
