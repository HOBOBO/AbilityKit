using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncReconcilePanel : IBattleDebugPanel
    {
        public string Name => "帧同步/对账";
        public int Order => 53;

        public bool IsVisible(in BattleDebugContext ctx)
        {
            return EditorApplication.isPlaying && BattleFlowDebugProvider.Current != null;
        }

        public void Draw(in BattleDebugContext ctx)
        {
            var flowCtx = BattleFlowDebugProvider.Current;
            if (flowCtx == null)
            {
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current 为空。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("对账目标", flowCtx.PredictionReconcileTarget != null ? "已设置" : "为空");

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("PredictionStats 为空。", MessageType.Info);
                return;
            }

            var wid = new WorldId(flowCtx.Plan.WorldId);

            if (flowCtx.PredictionStats.TryGetReconcileEnabled(wid, out var enabled))
            {
                EditorGUILayout.LabelField("对账是否启用（世界）", enabled.ToString());
            }

            EditorGUILayout.LabelField("不一致次数（总）", flowCtx.PredictionStats.TotalReconcileMismatch.ToString());
            EditorGUILayout.LabelField("最近不一致帧", flowCtx.PredictionStats.LastReconcileMismatchFrame.Value.ToString());
            EditorGUILayout.LabelField("最近预测哈希", flowCtx.PredictionStats.LastReconcilePredictedHash.Value.ToString());
            EditorGUILayout.LabelField("最近权威哈希", flowCtx.PredictionStats.LastReconcileAuthoritativeHash.Value.ToString());
            EditorGUILayout.LabelField("最近对比帧", flowCtx.PredictionStats.LastReconcileComparedFrame.Value.ToString());
            EditorGUILayout.LabelField("预测哈希记录次数（总）", flowCtx.PredictionStats.TotalPredictedHashRecorded.ToString());
            EditorGUILayout.LabelField("权威哈希跳过（无预测哈希）", flowCtx.PredictionStats.TotalAuthoritativeHashSkippedNoPredictedHash.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("回放超时次数（总）", flowCtx.PredictionStats.TotalReplayTimeout.ToString());
            EditorGUILayout.LabelField("回放超时最近帧", flowCtx.PredictionStats.LastReplayTimeoutFrame.Value.ToString());

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("权威哈希接收次数（总）", flowCtx.PredictionStats.TotalAuthoritativeHashReceived.ToString());
            EditorGUILayout.LabelField("最近权威哈希帧", flowCtx.PredictionStats.LastAuthoritativeHashFrame.Value.ToString());
            EditorGUILayout.LabelField("最近权威哈希", flowCtx.PredictionStats.LastAuthoritativeHash.Value.ToString());
            EditorGUILayout.LabelField("权威哈希忽略次数（无对账器）", flowCtx.PredictionStats.TotalAuthoritativeHashIgnoredNoReconciler.ToString());

            if (flowCtx.PredictionReconcileControl != null)
            {
                var swid = flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId : new WorldId(flowCtx.Plan.WorldId);

                if (flowCtx.PredictionReconcileControl.TryGetReconcileEnabled(swid, out var swEnabled))
                {
                    EditorGUILayout.LabelField("对账开关（运行时）", swEnabled.ToString());
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("恢复"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(swid, true);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("关闭对账"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(swid, false);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("开启对账"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(swid, true);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
