using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncNetworkPanel : IBattleDebugPanel
    {
        public string Name => "FrameSync/Network";
        public int Order => 55;

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

            var stats = BattleFlowDebugProvider.JitterBufferStats;
            if (stats == null)
            {
                EditorGUILayout.HelpBox("JitterBufferStats is null. (Not wired)", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("DelayFrames", stats.DelayFrames.ToString());
            EditorGUILayout.LabelField("MissingMode", stats.MissingMode);
            EditorGUILayout.LabelField("TargetFrame", stats.TargetFrame.ToString());
            EditorGUILayout.LabelField("MaxReceivedFrame", stats.MaxReceivedFrame.ToString());
            EditorGUILayout.LabelField("LastConsumedFrame", stats.LastConsumedFrame.ToString());
            EditorGUILayout.LabelField("BufferedCount", stats.BufferedCount.ToString());
            EditorGUILayout.LabelField("MinBufferedFrame", stats.MinBufferedFrame.ToString());

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Added", stats.AddedCount.ToString());
            EditorGUILayout.LabelField("Consumed", stats.ConsumedCount.ToString());
            EditorGUILayout.LabelField("Duplicate", stats.DuplicateCount.ToString());
            EditorGUILayout.LabelField("Late", stats.LateCount.ToString());
            EditorGUILayout.LabelField("FilledDefault", stats.FilledDefaultCount.ToString());
        }
    }
}
