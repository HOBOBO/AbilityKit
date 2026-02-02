using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncTimePanel : IBattleDebugPanel
    {
        public string Name => "帧同步/时间";
        public int Order => 54;

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

            var session = flowCtx.Session;
            if (session == null)
            {
                EditorGUILayout.HelpBox("BattleContext.Session 为空。", MessageType.Info);
                return;
            }

            if (!session.TryGetWorld(out var world) || world == null)
            {
                EditorGUILayout.HelpBox("当前没有活动世界。", MessageType.Info);
                return;
            }

            if (world.Services == null)
            {
                EditorGUILayout.HelpBox("World.Services 为空。", MessageType.Info);
                return;
            }

            if (!world.Services.TryResolve<IFrameTime>(out var time) || time == null)
            {
                EditorGUILayout.HelpBox("世界服务中未找到 IFrameTime。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("世界ID", world.Id.ToString());
            EditorGUILayout.LabelField("帧", time.Frame.Value.ToString());
            EditorGUILayout.LabelField("时间（秒）", time.Time.ToString("F3"));
            EditorGUILayout.LabelField("DeltaTime", time.DeltaTime.ToString("F4"));
            EditorGUILayout.LabelField("固定帧间隔", (time.FrameToTime(new FrameIndex(time.Frame.Value + 1)) - time.FrameToTime(time.Frame)).ToString("F4"));

            EditorGUILayout.Space();
            var ts = BattleFlowDebugProvider.TimeSyncStats;
            var map = BattleFlowDebugProvider.TimeSyncStatsByWorld;
            if (map != null)
            {
                EditorGUILayout.LabelField("TimeSyncStatsByWorld 数量", map.Count.ToString());
                var key = world.Id.ToString();
                if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var perWorld) && perWorld != null)
                {
                    ts = perWorld;
                }
            }
            if (ts == null)
            {
                EditorGUILayout.HelpBox("TimeSyncStats 为空（未接线）。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("时间同步 OpCode", ts.OpCode.ToString());
            EditorGUILayout.LabelField("时间同步间隔（ms）", ts.IntervalMs.ToString());
            EditorGUILayout.LabelField("时间同步 Alpha", ts.Alpha.ToString("F3"));
            EditorGUILayout.LabelField("时间同步超时（ms）", ts.TimeoutMs.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("锚点是否就绪", ts.HasAnchor.ToString());
            EditorGUILayout.LabelField("锚点起始帧", ts.AnchorStartFrame.ToString());
            EditorGUILayout.LabelField("锚点固定帧间隔（秒）", ts.AnchorFixedDeltaSeconds.ToString("F6"));
            EditorGUILayout.LabelField("服务器 Tick 频率", ts.AnchorServerTickFrequency.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("时钟同步是否就绪", ts.HasClockSync.ToString());
            EditorGUILayout.LabelField("时钟偏移（EWMA 秒）", ts.OffsetSecondsEwma.ToString("F6"));
            EditorGUILayout.LabelField("往返延迟（EWMA 秒）", ts.RttSecondsEwma.ToString("F6"));
            EditorGUILayout.LabelField("采样次数", ts.Samples.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("理想帧（原始）", ts.IdealFrameRaw.ToString());
            EditorGUILayout.LabelField("理想帧安全边界（帧）", ts.IdealFrameSafetyMarginFrames.ToString());
            EditorGUILayout.LabelField("理想帧上限", ts.IdealFrameLimit.ToString());
        }
    }
}
