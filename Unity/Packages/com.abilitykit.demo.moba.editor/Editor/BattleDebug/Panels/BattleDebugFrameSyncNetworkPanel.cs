using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncNetworkPanel : IBattleDebugPanel
    {
        public string Name => "帧同步/网络";
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
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current 为空。", MessageType.Info);
                return;
            }

            var stats = BattleFlowDebugProvider.JitterBufferStats;
            if (stats == null)
            {
                EditorGUILayout.HelpBox("JitterBufferStats 为空（未接线）。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("网络缓冲区（JitterBuffer）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("延迟帧数", stats.DelayFrames.ToString());
            EditorGUILayout.LabelField("缺帧处理模式", stats.MissingMode);
            EditorGUILayout.LabelField("目标帧", stats.TargetFrame.ToString());
            EditorGUILayout.LabelField("已接收最大帧", stats.MaxReceivedFrame.ToString());
            EditorGUILayout.LabelField("最近消耗帧", stats.LastConsumedFrame.ToString());
            EditorGUILayout.LabelField("缓冲数量", stats.BufferedCount.ToString());
            EditorGUILayout.LabelField("最小缓冲帧", stats.MinBufferedFrame.ToString());

            // Visualize buffered fill vs delay frames.
            var denom = stats.DelayFrames > 0 ? stats.DelayFrames : 1;
            var ratio = Mathf.Clamp01(stats.BufferedCount / (float)denom);
            var r0 = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r0, ratio, $"缓冲填充：{stats.BufferedCount}/{denom}");

            // Visualize how far target frame is from consumed.
            var gapToTarget = stats.TargetFrame - stats.LastConsumedFrame;
            var gapRatio = Mathf.Clamp01(gapToTarget / (float)denom);
            var r1 = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r1, gapRatio, $"目标帧差：{gapToTarget}/{denom}（目标-已消耗）");

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("统计", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("入队", stats.AddedCount.ToString());
            EditorGUILayout.LabelField("出队/消耗", stats.ConsumedCount.ToString());
            EditorGUILayout.LabelField("重复包", stats.DuplicateCount.ToString());
            EditorGUILayout.LabelField("迟到包", stats.LateCount.ToString());
            EditorGUILayout.LabelField("缺帧填充（默认）", stats.FilledDefaultCount.ToString());
        }
    }
}
