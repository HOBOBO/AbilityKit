using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncReconcilePanel : IBattleDebugPanel
    {
        public string Name => "FrameSync/Reconcile";
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
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current is null.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("ReconcileTarget", flowCtx.PredictionReconcileTarget != null ? "set" : "null");

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("PredictionStats is null.", MessageType.Info);
                return;
            }

            var wid = new WorldId(flowCtx.Plan.WorldId);

            if (flowCtx.PredictionStats.TryGetReconcileEnabled(wid, out var enabled))
            {
                EditorGUILayout.LabelField("ReconcileEnabled", enabled.ToString());
            }

            EditorGUILayout.LabelField("MismatchTotal", flowCtx.PredictionStats.TotalReconcileMismatch.ToString());
            EditorGUILayout.LabelField("LastMismatchFrame", flowCtx.PredictionStats.LastReconcileMismatchFrame.Value.ToString());
            EditorGUILayout.LabelField("LastPredHash", flowCtx.PredictionStats.LastReconcilePredictedHash.Value.ToString());
            EditorGUILayout.LabelField("LastAuthHash", flowCtx.PredictionStats.LastReconcileAuthoritativeHash.Value.ToString());
            EditorGUILayout.LabelField("LastComparedFrame", flowCtx.PredictionStats.LastReconcileComparedFrame.Value.ToString());
            EditorGUILayout.LabelField("PredHashRecorded", flowCtx.PredictionStats.TotalPredictedHashRecorded.ToString());
            EditorGUILayout.LabelField("AuthSkippedNoPred", flowCtx.PredictionStats.TotalAuthoritativeHashSkippedNoPredictedHash.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ReplayTimeout.total", flowCtx.PredictionStats.TotalReplayTimeout.ToString());
            EditorGUILayout.LabelField("ReplayTimeout.lastFrame", flowCtx.PredictionStats.LastReplayTimeoutFrame.Value.ToString());

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("AuthHashRxTotal", flowCtx.PredictionStats.TotalAuthoritativeHashReceived.ToString());
            EditorGUILayout.LabelField("AuthHashLastFrame", flowCtx.PredictionStats.LastAuthoritativeHashFrame.Value.ToString());
            EditorGUILayout.LabelField("AuthHashLast", flowCtx.PredictionStats.LastAuthoritativeHash.Value.ToString());
            EditorGUILayout.LabelField("AuthHashIgnoredNoReconciler", flowCtx.PredictionStats.TotalAuthoritativeHashIgnoredNoReconciler.ToString());

            if (flowCtx.PredictionReconcileControl != null)
            {
                var swid = flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId : new WorldId(flowCtx.Plan.WorldId);

                if (flowCtx.PredictionReconcileControl.TryGetReconcileEnabled(swid, out var swEnabled))
                {
                    EditorGUILayout.LabelField("ReconcileSwitch", swEnabled.ToString());
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Recover"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(swid, true);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("Disable Reconcile"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(swid, false);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.WorldId));
                }

                if (GUILayout.Button("Enable Reconcile"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(swid, true);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
