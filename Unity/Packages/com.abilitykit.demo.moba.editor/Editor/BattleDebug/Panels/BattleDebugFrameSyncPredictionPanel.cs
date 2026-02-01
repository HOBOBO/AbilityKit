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

        public string Name => "FrameSync/Prediction";
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
                EditorGUILayout.HelpBox("BattleFlowDebugProvider.Current is null.", MessageType.Info);
                return;
            }

            if (flowCtx.PredictionStats == null)
            {
                EditorGUILayout.HelpBox("PredictionStats is null.", MessageType.Info);
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
                EditorGUILayout.LabelField("Tuning (Global)");
                _editMaxAhead = EditorGUILayout.IntField("MaxAhead", _editMaxAhead);
                _editMinWindow = EditorGUILayout.IntField("MinWindow", _editMinWindow);
                _editAlpha = EditorGUILayout.FloatField("BacklogAlpha", _editAlpha);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply"))
                {
                    flowCtx.PredictionTuningControl.SetMaxPredictionAheadFrames(_editMaxAhead);
                    flowCtx.PredictionTuningControl.SetMinPredictionWindow(_editMinWindow);
                    flowCtx.PredictionTuningControl.SetBacklogEwmaAlpha(_editAlpha);
                }
                if (GUILayout.Button("Reset"))
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

            EditorGUILayout.LabelField("MaxPredictionAhead", flowCtx.PredictionStats.MaxPredictionAheadFrames.ToString());
            EditorGUILayout.LabelField("MinPredWindow", flowCtx.PredictionStats.MinPredictionWindow.ToString());
            EditorGUILayout.LabelField("BacklogAlpha", flowCtx.PredictionStats.BacklogEwmaAlpha.ToString("F2"));

            var wid = new WorldId(flowCtx.Plan.WorldId);

            if (flowCtx.PredictionStats.TryGetPredictionWindowStats(wid, out var backlogRaw, out var backlogEwma, out var window, out var stalled))
            {
                EditorGUILayout.LabelField("Backlog.raw", backlogRaw.ToString());
                EditorGUILayout.LabelField("Backlog.ewma", backlogEwma.ToString("F2"));
                EditorGUILayout.LabelField("CurrentPredWindow", window.ToString());
                EditorGUILayout.LabelField("PredWindowStalled", stalled.ToString());

                if (flowCtx.PredictionStats.TryGetPredictionWindowStats(wid, out _, out _, out _, out _, out var stallsTotal))
                {
                    EditorGUILayout.LabelField("PredWindowStallsTotal(world)", stallsTotal.ToString());
                }
            }
            else
            {
                EditorGUILayout.LabelField("Backlog.raw", flowCtx.PredictionStats.CurrentBacklogRaw.ToString());
                EditorGUILayout.LabelField("Backlog.ewma", flowCtx.PredictionStats.CurrentBacklogEwma.ToString("F2"));
                EditorGUILayout.LabelField("CurrentPredWindow", flowCtx.PredictionStats.CurrentPredictionWindow.ToString());
                EditorGUILayout.LabelField("PredWindowStalled", flowCtx.PredictionStats.IsPredictionStalledByWindow.ToString());
            }

            EditorGUILayout.LabelField("PredWindowStallsTotal(global)", flowCtx.PredictionStats.TotalPredictionWindowStalls.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("IdealFrameLimit", flowCtx.PredictionStats.CurrentIdealFrameLimit.ToString());
            EditorGUILayout.LabelField("IdealFrameStalled", flowCtx.PredictionStats.IsPredictionStalledByIdealFrame.ToString());
            EditorGUILayout.LabelField("IdealFrameStallsTotal", flowCtx.PredictionStats.TotalIdealFrameStalls.ToString());

            if (flowCtx.PredictionStats.TryGetIdealFrameStallStats(wid, out var idealLimitWorld, out var idealStalledWorld, out var idealStallsTotalWorld))
            {
                EditorGUILayout.LabelField("IdealFrameLimit(world)", idealLimitWorld.ToString());
                EditorGUILayout.LabelField("IdealFrameStalled(world)", idealStalledWorld.ToString());
                EditorGUILayout.LabelField("IdealFrameStallsTotal(world)", idealStallsTotalWorld.ToString());
            }


            if (flowCtx.PredictionStats.TryGetFrames(wid, out var confirmed, out var predicted))
            {
                EditorGUILayout.LabelField("Frames", $"confirmed={confirmed.Value} predicted={predicted.Value}");
            }
            else
            {
                EditorGUILayout.LabelField("Frames", "no world context");
            }

            EditorGUILayout.LabelField("InputDelayFrames", flowCtx.PredictionStats.InputDelayFrames.ToString());
            EditorGUILayout.LabelField("LastConsumed", $"pred={flowCtx.PredictionStats.LastConsumedPredictedFrames} conf={flowCtx.PredictionStats.LastConsumedConfirmedFrames}");
            EditorGUILayout.LabelField("Totals", $"drops={flowCtx.PredictionStats.TotalLocalDelayQueueDroppedBatches} totalPred={flowCtx.PredictionStats.TotalPredictedFrames} totalConf={flowCtx.PredictionStats.TotalConsumedConfirmedFrames}");

            if (flowCtx.PredictionStats.TryGetLocalDelayQueueDepth(wid, out var depth))
            {
                EditorGUILayout.LabelField("DelayQueueDepth", depth.ToString());
            }
        }
    }
}
