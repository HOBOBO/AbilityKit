using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncPanel : IBattleDebugPanel
    {
        public string Name => "帧同步/总览";
        public int Order => 50;

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

            EditorGUILayout.LabelField("世界ID", flowCtx.Plan.WorldId.ToString());
            EditorGUILayout.LabelField("最近帧", flowCtx.LastFrame.ToString());

            EditorGUILayout.LabelField("运行时世界ID", flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId.ToString() : "（无）");

            if (flowCtx.PredictionReconcileControl != null)
            {
                var wid = flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId : new WorldId(flowCtx.Plan.WorldId);

                if (flowCtx.PredictionReconcileControl.TryGetReconcileEnabled(wid, out var enabled))
                {
                    EditorGUILayout.LabelField("对账开关", enabled.ToString());
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("恢复"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, true);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("关闭对账"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, false);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("开启对账"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, true);
                }
                EditorGUILayout.EndHorizontal();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EditorGUILayout.LabelField("强制哈希不一致（调试）", BattleSessionFeature.DebugForceClientHashMismatch.ToString());
            if (GUILayout.Button("切换：强制哈希不一致"))
            {
                BattleSessionFeature.DebugForceClientHashMismatch = !BattleSessionFeature.DebugForceClientHashMismatch;

                if (flowCtx.PredictionReconcileControl != null)
                {
                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }
            }
#endif

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("对账目标", flowCtx.PredictionReconcileTarget != null ? "已设置" : "为空");

            if (flowCtx.PredictionStats != null)
            {
                var wid = new WorldId(flowCtx.Plan.WorldId);
                if (flowCtx.PredictionStats.TryGetFrames(wid, out var confirmed, out var predicted))
                {
                    EditorGUILayout.LabelField("帧", $"确认={confirmed.Value} 预测={predicted.Value}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("回放超时次数（总）", flowCtx.PredictionStats.TotalReplayTimeout.ToString());
                EditorGUILayout.LabelField("回放超时最近帧", flowCtx.PredictionStats.LastReplayTimeoutFrame.Value.ToString());
                EditorGUILayout.LabelField("因回放超时自动关闭对账（总）", flowCtx.PredictionStats.TotalReconcileAutoDisabledByReplayTimeout.ToString());
                EditorGUILayout.LabelField("因回放超时自动关闭对账最近帧", flowCtx.PredictionStats.LastReconcileAutoDisabledByReplayTimeoutFrame.Value.ToString());
            }
        }
    }
}
