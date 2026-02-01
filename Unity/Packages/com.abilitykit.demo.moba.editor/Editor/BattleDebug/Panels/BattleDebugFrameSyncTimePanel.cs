using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncTimePanel : IBattleDebugPanel
    {
        public string Name => "FrameSync/Time";
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
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current is null.", MessageType.Info);
                return;
            }

            var session = flowCtx.Session;
            if (session == null)
            {
                EditorGUILayout.HelpBox("BattleContext.Session is null.", MessageType.Info);
                return;
            }

            if (!session.TryGetWorld(out var world) || world == null)
            {
                EditorGUILayout.HelpBox("No active world.", MessageType.Info);
                return;
            }

            if (world.Services == null)
            {
                EditorGUILayout.HelpBox("World.Services is null.", MessageType.Info);
                return;
            }

            if (!world.Services.TryResolve<IFrameTime>(out var time) || time == null)
            {
                EditorGUILayout.HelpBox("IFrameTime not found in world services.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("WorldId", world.Id.ToString());
            EditorGUILayout.LabelField("Frame", time.Frame.Value.ToString());
            EditorGUILayout.LabelField("Time", time.Time.ToString("F3"));
            EditorGUILayout.LabelField("DeltaTime", time.DeltaTime.ToString("F4"));
            EditorGUILayout.LabelField("FixedDelta", (time.FrameToTime(new FrameIndex(time.Frame.Value + 1)) - time.FrameToTime(time.Frame)).ToString("F4"));

            EditorGUILayout.Space();
            var ts = BattleFlowDebugProvider.TimeSyncStats;
            var map = BattleFlowDebugProvider.TimeSyncStatsByWorld;
            if (map != null)
            {
                EditorGUILayout.LabelField("TimeSyncStatsByWorld.Count", map.Count.ToString());
                var key = world.Id.ToString();
                if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var perWorld) && perWorld != null)
                {
                    ts = perWorld;
                }
            }
            if (ts == null)
            {
                EditorGUILayout.HelpBox("TimeSyncStats is null. (Not wired)", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("TimeSync.OpCode", ts.OpCode.ToString());
            EditorGUILayout.LabelField("TimeSync.IntervalMs", ts.IntervalMs.ToString());
            EditorGUILayout.LabelField("TimeSync.Alpha", ts.Alpha.ToString("F3"));
            EditorGUILayout.LabelField("TimeSync.TimeoutMs", ts.TimeoutMs.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Anchor.Has", ts.HasAnchor.ToString());
            EditorGUILayout.LabelField("Anchor.StartFrame", ts.AnchorStartFrame.ToString());
            EditorGUILayout.LabelField("Anchor.FixedDeltaSeconds", ts.AnchorFixedDeltaSeconds.ToString("F6"));
            EditorGUILayout.LabelField("Anchor.ServerTickFrequency", ts.AnchorServerTickFrequency.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ClockSync.Has", ts.HasClockSync.ToString());
            EditorGUILayout.LabelField("ClockSync.OffsetSeconds(EWMA)", ts.OffsetSecondsEwma.ToString("F6"));
            EditorGUILayout.LabelField("ClockSync.RttSeconds(EWMA)", ts.RttSecondsEwma.ToString("F6"));
            EditorGUILayout.LabelField("ClockSync.Samples", ts.Samples.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("IdealFrame.Raw", ts.IdealFrameRaw.ToString());
            EditorGUILayout.LabelField("IdealFrame.MarginFrames", ts.IdealFrameSafetyMarginFrames.ToString());
            EditorGUILayout.LabelField("IdealFrame.Limit", ts.IdealFrameLimit.ToString());
        }
    }
}
