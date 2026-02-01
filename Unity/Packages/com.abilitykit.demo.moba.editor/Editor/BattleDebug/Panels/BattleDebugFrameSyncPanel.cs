using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncPanel : IBattleDebugPanel
    {
        public string Name => "FrameSync/Overview";
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
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current is null.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("WorldId", flowCtx.Plan.WorldId.ToString());
            EditorGUILayout.LabelField("LastFrame", flowCtx.LastFrame.ToString());

            EditorGUILayout.LabelField("RuntimeWorldId", flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId.ToString() : "(none)");

            if (flowCtx.PredictionReconcileControl != null)
            {
                var wid = flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId : new WorldId(flowCtx.Plan.WorldId);

                if (flowCtx.PredictionReconcileControl.TryGetReconcileEnabled(wid, out var enabled))
                {
                    EditorGUILayout.LabelField("ReconcileSwitch", enabled.ToString());
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Recover"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, true);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("Disable Reconcile"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, false);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("Enable Reconcile"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, true);
                }
                EditorGUILayout.EndHorizontal();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EditorGUILayout.LabelField("DebugForceHashMismatch", BattleSessionFeature.DebugForceClientHashMismatch.ToString());
            if (GUILayout.Button("Toggle ForceHashMismatch"))
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
            EditorGUILayout.LabelField("ReconcileTarget", flowCtx.PredictionReconcileTarget != null ? "set" : "null");

            if (flowCtx.PredictionStats != null)
            {
                var wid = new WorldId(flowCtx.Plan.WorldId);
                if (flowCtx.PredictionStats.TryGetFrames(wid, out var confirmed, out var predicted))
                {
                    EditorGUILayout.LabelField("Frames", $"confirmed={confirmed.Value} predicted={predicted.Value}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("ReplayTimeout.total", flowCtx.PredictionStats.TotalReplayTimeout.ToString());
                EditorGUILayout.LabelField("ReplayTimeout.lastFrame", flowCtx.PredictionStats.LastReplayTimeoutFrame.Value.ToString());
                EditorGUILayout.LabelField("AutoDisableReconcile.total", flowCtx.PredictionStats.TotalReconcileAutoDisabledByReplayTimeout.ToString());
                EditorGUILayout.LabelField("AutoDisableReconcile.lastFrame", flowCtx.PredictionStats.LastReconcileAutoDisabledByReplayTimeoutFrame.Value.ToString());
            }
        }
    }
}
