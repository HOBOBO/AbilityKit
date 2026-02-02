using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugFrameSyncPredictionPanel : IBattleDebugPanel
    {
        private bool _tuningUiInitialized;
        private int _editMaxAhead;
        private int _editMinWindow;
        private float _editAlpha;

        private static void DrawMarker(ref Rect r, float t, Color color)
        {
            t = Mathf.Clamp01(t);
            var x = Mathf.Lerp(r.xMin, r.xMax, t);
            var mr = new Rect(x - 1f, r.yMin, 2f, r.height);
            EditorGUI.DrawRect(mr, color);
        }

        private static void DrawFramesMarkerBar(int minFrame, int maxFrame, int confirmed, int authoritative, int predicted)
        {
            var span = maxFrame - minFrame;
            if (span <= 0)
            {
                span = 1;
            }

            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r, 1f, $"帧位置（区间）：{minFrame} - {maxFrame}（跨度 {span}）");

            DrawMarker(ref r, (confirmed - minFrame) / (float)span, new Color(0.25f, 0.55f, 1f, 1f));
            DrawMarker(ref r, (authoritative - minFrame) / (float)span, new Color(0.95f, 0.75f, 0.2f, 1f));
            DrawMarker(ref r, (predicted - minFrame) / (float)span, new Color(0.2f, 0.9f, 0.4f, 1f));

            var legendR = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.LabelField(
                legendR,
                "图例：蓝=已确认  黄=权威最新  绿=本地预测");
        }

        public string Name => "帧同步/预测";
        public int Order => 51;

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

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("PredictionStats 为空。", MessageType.Info);
                return;
            }

            if (flowCtx.PredictionTuningControl != null)
            {
                if (!_tuningUiInitialized)
                {
                    _editMaxAhead = flowCtx.PredictionTuningControl.MaxPredictionAheadFrames;
                    _editMinWindow = flowCtx.PredictionTuningControl.MinPredictionWindow;
                    _editAlpha = flowCtx.PredictionTuningControl.BacklogEwmaAlpha;
                    _tuningUiInitialized = true;
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("调参（全局）");
                _editMaxAhead = EditorGUILayout.IntField("最大超前帧数", _editMaxAhead);
                _editMinWindow = EditorGUILayout.IntField("最小预测窗口", _editMinWindow);
                _editAlpha = EditorGUILayout.FloatField("积压平滑系数（EWMA Alpha）", _editAlpha);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("应用"))
                {
                    flowCtx.PredictionTuningControl.SetMaxPredictionAheadFrames(_editMaxAhead);
                    flowCtx.PredictionTuningControl.SetMinPredictionWindow(_editMinWindow);
                    flowCtx.PredictionTuningControl.SetBacklogEwmaAlpha(_editAlpha);
                }
                if (GUILayout.Button("重置"))
                {
                    flowCtx.PredictionTuningControl.ResetDefaults();
                    _editMaxAhead = flowCtx.PredictionTuningControl.MaxPredictionAheadFrames;
                    _editMinWindow = flowCtx.PredictionTuningControl.MinPredictionWindow;
                    _editAlpha = flowCtx.PredictionTuningControl.BacklogEwmaAlpha;
                    _tuningUiInitialized = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
            }

            EditorGUILayout.LabelField("最大预测超前帧数", flowCtx.PredictionStats.MaxPredictionAheadFrames.ToString());
            EditorGUILayout.LabelField("最小预测窗口", flowCtx.PredictionStats.MinPredictionWindow.ToString());
            EditorGUILayout.LabelField("积压平滑系数（Alpha）", flowCtx.PredictionStats.BacklogEwmaAlpha.ToString("F2"));

            var wid = new WorldId(flowCtx.Plan.WorldId);

            if (flowCtx.PredictionStats.TryGetFrames(wid, out var confirmedFrame, out var predictedFrame))
            {
                var confirmed = confirmedFrame.Value;
                var predicted = predictedFrame.Value;
                var deltaPredictedToConfirmed = predicted - confirmed;

                // Third frame: best-effort authoritative latest frame from jitter buffer (if wired).
                // If not available, fall back to confirmed.
                var jb = BattleFlowDebugProvider.JitterBufferStats;
                var authoritative = jb != null ? jb.MaxReceivedFrame : confirmed;
                var deltaAuthoritativeToConfirmed = authoritative - confirmed;
                var deltaPredictedToAuthoritative = predicted - authoritative;

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("帧位置对比（已确认/权威最新/本地预测）", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("已确认帧（权威）", confirmed.ToString());
                EditorGUILayout.LabelField("权威最新已接收帧", authoritative.ToString());
                EditorGUILayout.LabelField("本地预测帧", predicted.ToString());
                EditorGUILayout.LabelField("差值（权威最新-已确认）", deltaAuthoritativeToConfirmed.ToString());
                EditorGUILayout.LabelField("差值（预测-已确认）", deltaPredictedToConfirmed.ToString());
                EditorGUILayout.LabelField("差值（预测-权威最新）", deltaPredictedToAuthoritative.ToString());

                var minFrame = Mathf.Min(confirmed, Mathf.Min(authoritative, predicted));
                var maxFrame = Mathf.Max(confirmed, Mathf.Max(authoritative, predicted));
                DrawFramesMarkerBar(minFrame, maxFrame, confirmed, authoritative, predicted);

                var maxAhead = flowCtx.PredictionStats.MaxPredictionAheadFrames;
                var denom = maxAhead > 0 ? maxAhead : 1;
                var ratio = Mathf.Clamp01(deltaPredictedToConfirmed / (float)denom);
                var r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(r, ratio, $"预测超前占比：{deltaPredictedToConfirmed}/{denom}");
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("帧位置对比（已确认/权威最新/本地预测）", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("当前没有世界上下文，无法获取 confirmed/predicted 帧。", MessageType.Info);
            }

            if (flowCtx.PredictionStats.TryGetPredictionWindowStats(wid, out var backlogRaw, out var backlogEwma, out var window, out var stalled))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("预测窗口/积压", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("积压（raw）", backlogRaw.ToString());
                EditorGUILayout.LabelField("积压（ewma）", backlogEwma.ToString("F2"));
                EditorGUILayout.LabelField("当前预测窗口", window.ToString());
                EditorGUILayout.LabelField("预测窗口是否阻塞", stalled.ToString());

                var denom = window > 0 ? window : 1;
                var ratio = Mathf.Clamp01(backlogRaw / (float)denom);
                var r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(r, ratio, $"窗口填充：{backlogRaw}/{denom}");

                if (flowCtx.PredictionStats.TryGetPredictionWindowStats(wid, out _, out _, out _, out _, out var stallsTotal))
                {
                    EditorGUILayout.LabelField("预测窗口阻塞次数（世界）", stallsTotal.ToString());
                }
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("预测窗口/积压", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("积压（raw）", flowCtx.PredictionStats.CurrentBacklogRaw.ToString());
                EditorGUILayout.LabelField("积压（ewma）", flowCtx.PredictionStats.CurrentBacklogEwma.ToString("F2"));
                EditorGUILayout.LabelField("当前预测窗口", flowCtx.PredictionStats.CurrentPredictionWindow.ToString());
                EditorGUILayout.LabelField("预测窗口是否阻塞", flowCtx.PredictionStats.IsPredictionStalledByWindow.ToString());

                var denom = flowCtx.PredictionStats.CurrentPredictionWindow > 0 ? flowCtx.PredictionStats.CurrentPredictionWindow : 1;
                var ratio = Mathf.Clamp01(flowCtx.PredictionStats.CurrentBacklogRaw / (float)denom);
                var r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(r, ratio, $"窗口填充：{flowCtx.PredictionStats.CurrentBacklogRaw}/{denom}");
            }

            EditorGUILayout.LabelField("预测窗口阻塞次数（全局）", flowCtx.PredictionStats.TotalPredictionWindowStalls.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("理想帧上限", flowCtx.PredictionStats.CurrentIdealFrameLimit.ToString());
            EditorGUILayout.LabelField("是否因理想帧阻塞", flowCtx.PredictionStats.IsPredictionStalledByIdealFrame.ToString());
            EditorGUILayout.LabelField("理想帧阻塞次数（全局）", flowCtx.PredictionStats.TotalIdealFrameStalls.ToString());

            if (flowCtx.PredictionStats.TryGetIdealFrameStallStats(wid, out var idealLimitWorld, out var idealStalledWorld, out var idealStallsTotalWorld))
            {
                EditorGUILayout.LabelField("理想帧上限（世界）", idealLimitWorld.ToString());
                EditorGUILayout.LabelField("是否因理想帧阻塞（世界）", idealStalledWorld.ToString());
                EditorGUILayout.LabelField("理想帧阻塞次数（世界）", idealStallsTotalWorld.ToString());
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输入延迟/本地队列", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入延迟帧数", flowCtx.PredictionStats.InputDelayFrames.ToString());
            EditorGUILayout.LabelField("最近消耗帧", $"预测={flowCtx.PredictionStats.LastConsumedPredictedFrames} 确认={flowCtx.PredictionStats.LastConsumedConfirmedFrames}");
            EditorGUILayout.LabelField("统计", $"丢弃批次={flowCtx.PredictionStats.TotalLocalDelayQueueDroppedBatches} 预测总帧={flowCtx.PredictionStats.TotalPredictedFrames} 消耗确认总帧={flowCtx.PredictionStats.TotalConsumedConfirmedFrames}");

            if (flowCtx.PredictionStats.TryGetLocalDelayQueueDepth(wid, out var depth))
            {
                EditorGUILayout.LabelField("延迟队列深度", depth.ToString());

                var denom = flowCtx.PredictionStats.MinPredictionWindow > 0 ? flowCtx.PredictionStats.MinPredictionWindow : 1;
                var ratio = Mathf.Clamp01(depth / (float)denom);
                var r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(r, ratio, $"延迟队列占比：{depth}/{denom}");
            }
        }
    }
}
