#if UNITY_EDITOR
#nullable enable annotations
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BattleScenario.Editor
{
    /// <summary>
    /// 复用同一套播放控件，只读展示草稿演练或测试产物的重演过程和交付检查点。
    /// 播放频率只控制画面，不执行场景命令或状态同步。
    /// </summary>
    internal sealed class BattleScenarioLocalReplayWindow : EditorWindow
    {
        private const double SecondsPerStep = 0.65;
        private const string AcceptanceDirectoryKey = "AbilityKit.BattleScenario.LocalReplay.Directory";
        private static readonly string[] SpeedLabels = { "0.5×", "1×", "2×" };
        private static readonly double[] Speeds = { 0.5, 1, 2 };
        private IReadOnlyList<LocalCorrectionPreview.Frame> _frames = Array.Empty<LocalCorrectionPreview.Frame>();
        private LocalStateSyncReplay? _acceptance;
        private LocalStateSyncReplay.Timeline? _acceptanceTimeline;
        private string[] _deliveryLabels = Array.Empty<string>();
        private string _acceptanceSource = string.Empty;
        private string? _acceptanceLabel;
        private string? _importError;
        private string _caseId = string.Empty;
        private int _index;
        private int _speedIndex = 1;
        private bool _playing;
        private double _nextStepAt;
        private int _minPosition;
        private int _maxPosition;
        private Vector2 _detailScroll;
        private bool _showActorProjection;
        private bool _hasRecordedActorPose;
        private IActorReplayPreview? _actorProjection;
        private Bounds _recordedBounds;
        private int FrameCount => _acceptanceTimeline?.Frames.Count ?? _frames.Count;
        private double StepInterval => SecondsPerStep / Speeds[_speedIndex];

        internal static void Open(string caseId, IReadOnlyList<LocalCorrectionPreview.Frame> frames)
        {
            var window = GetWindow<BattleScenarioLocalReplayWindow>();
            window.titleContent = new GUIContent("本地纠偏回放");
            window.minSize = new Vector2(425, 245);
            window._acceptance = null;
            window._acceptanceTimeline = null;
            window._deliveryLabels = Array.Empty<string>();
            window._acceptanceSource = string.Empty;
            window._acceptanceLabel = null;
            window._importError = null;
            window._frames = frames;
            window.ResetTimeline(caseId);
            foreach (var frame in frames)
            {
                window.IncludePosition(frame.ViewPosition);
                if (frame.AuthorityPosition is int authority) window.IncludePosition(authority);
            }
            window.StartPlaying();
            window.Show();
        }

        internal static void OpenAcceptance(LocalStateSyncReplay replay, string source, string? label = null)
        {
            var window = GetWindow<BattleScenarioLocalReplayWindow>();
            window.SetAcceptance(replay, source, label);
            window.Show();
        }

        internal static LocalStateSyncReplay? SelectAcceptance(out string source, out string? error)
        {
            var directory = SessionState.GetString(AcceptanceDirectoryKey, string.Empty);
            source = string.Empty;
            error = null;
            var path = EditorUtility.OpenFilePanel("导入本项目的状态同步验收回放", directory, "json");
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                if (new FileInfo(path).Length > 1024 * 1024)
                    throw new InvalidDataException("验收回放超过 1 MiB，请重新导出单个场景变体。");

                var replay = LocalStateSyncReplay.Parse(File.ReadAllText(path));
                source = path;
                SessionState.SetString(AcceptanceDirectoryKey, Path.GetDirectoryName(path) ?? string.Empty);
                return replay;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                error = "验收回放未导入：" + exception.Message;
                return null;
            }
        }

        private void SetAcceptance(LocalStateSyncReplay replay, string source, string? label = null)
        {
            var timeline = replay.BuildTimeline();
            var matchingIndex = _acceptanceTimeline == null ? null : timeline.FindMatchingCheckpoint(_acceptanceTimeline, _index);
            titleContent = new GUIContent("状态同步验收回放");
            minSize = new Vector2(530, 420);
            _acceptance = replay;
            _acceptanceTimeline = timeline;
            _acceptanceSource = source;
            _acceptanceLabel = label;
            _importError = null;
            _frames = Array.Empty<LocalCorrectionPreview.Frame>();
            _deliveryLabels = new string[replay.Checkpoints.Length];
            ResetTimeline(replay.CaseId);
            for (var index = 0; index < replay.Checkpoints.Length; index++)
            {
                var point = replay.Checkpoints[index];
                var replaySteps = point.ReplayTicks.Length == 0 ? string.Empty : $" · 重演 {point.ReplayTicks.Length} Tick";
                _deliveryLabels[index] = $"{index + 1}. Tick {point.DeliveryTick} · " +
                    (point.Presentation is { } sample ? DescribePresentationPhase(sample.Phase) : point.Command + replaySteps);
                IncludePosition(point.Presentation?.Authority?.Z);
                IncludePosition(point.Presentation?.View?.Z);
                IncludePosition(point.PublishedPosition);
                IncludePosition(point.Authority);
                IncludePosition(point.Prediction);
                IncludePosition(point.Commit?.MaterializedPosition);
            }
            foreach (var frame in timeline.Frames)
            {
                IncludePosition(frame.Position);
                IncludePose(frame.Point.AuthorityPose);
                IncludePose(frame.PredictionPose);
            }

            void IncludePose(LocalStateSyncReplay.ActorPose? pose)
            {
                if (ToUnityPose(pose) is not Pose observed) return;
                if (_hasRecordedActorPose) _recordedBounds.Encapsulate(observed.position);
                else _recordedBounds = new Bounds(observed.position, Vector3.zero);
                _hasRecordedActorPose = true;
            }
            Seek(matchingIndex ?? 0);
        }

        private void ResetTimeline(string caseId)
        {
            CloseActorProjection();
            _hasRecordedActorPose = false;
            _caseId = caseId;
            _index = 0;
            _playing = false;
            _detailScroll = Vector2.zero;
            _minPosition = int.MaxValue;
            _maxPosition = int.MinValue;
            Repaint();
        }

        private void OnEnable() => EditorApplication.update += Tick;

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            _playing = false;
            CloseActorProjection();
        }

        private void CloseActorProjection()
        {
            _showActorProjection = false;
            _actorProjection?.Dispose();
            _actorProjection = null;
        }

        private void StartPlaying()
        {
            if (_index == FrameCount - 1) _index = 0;
            _playing = FrameCount > 1;
            _nextStepAt = EditorApplication.timeSinceStartup + StepInterval;
            Repaint();
        }

        private void Tick()
        {
            if (!_playing) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _playing = false;
                Repaint();
                return;
            }
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextStepAt) return;
            _index++;
            _playing = _index < FrameCount - 1;
            _nextStepAt = now + StepInterval;
            Repaint();
        }

        private void OnGUI()
        {
            if (_acceptance is { } replay)
            {
                DrawAcceptanceHeader(replay);
                if (!string.IsNullOrWhiteSpace(replay.PositionLabel))
                    EditorGUILayout.LabelField("坐标刻度", replay.PositionLabel);
                EditorGUILayout.HelpBox(replay.HasPresentationTrack
                    ? "仅验收离线表现对账。权威轨道是独立逻辑世界观测，不代表本地已收到确认；不演示逻辑实体回滚。"
                    : replay.HasStepPositions
                    ? "播放本地测试记录的实际步进和检查点。重演位置只是候选值，最终预测以交付检查点为准。"
                    : "旧版产物只有交付检查点。重新导出后可单步查看重演位置。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("本地移动纠偏演练", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("回放的是草稿生成的固定步骤；不运行游戏或真实同步。修改草稿后需重新生成。", MessageType.Info);
            }
            if (FrameCount == 0) return;

            if (_acceptanceTimeline is { } timeline)
                DrawDeliveryNavigation(timeline);
            else
                EditorGUILayout.LabelField("用例", _caseId);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("回到起点")) Seek(0);
                using (new EditorGUI.DisabledScope(_index == 0))
                    if (GUILayout.Button("上一步")) Seek(_index - 1);
                using (new EditorGUI.DisabledScope(FrameCount <= 1 || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(_playing ? "暂停" : "播放"))
                    {
                        if (_playing) _playing = false;
                        else StartPlaying();
                    }
                using (new EditorGUI.DisabledScope(_index == FrameCount - 1))
                    if (GUILayout.Button("下一步")) Seek(_index + 1);

                var speedIndex = EditorGUILayout.Popup(_speedIndex, SpeedLabels, GUILayout.Width(60));
                if (speedIndex != _speedIndex)
                {
                    _speedIndex = speedIndex;
                    _nextStepAt = EditorApplication.timeSinceStartup + StepInterval;
                }
            }

            var target = EditorGUILayout.IntSlider("回放步骤", _index + 1, 1, FrameCount) - 1;
            if (target != _index) Seek(target);
            if (_acceptanceTimeline is { } current)
            {
                DrawActorProjection(current.Frames[_index]);
                DrawAcceptanceFrame(current.Frames[_index]);
                return;
            }
            var frame = _frames[_index];
            EditorGUILayout.LabelField(Label(frame.Stage), $"虚拟 Tick {frame.AtTick} · 模拟 Tick {frame.SimulationTick}");
            if (frame.PreviousAnchorPosition is int predictedAnchor && frame.AuthorityPosition is int authority)
                EditorGUILayout.LabelField("锚点对照", $"原预测 {predictedAnchor} · 权威 {authority}");
            DrawLane("画面位置", frame.ViewPosition, new Color(0.30f, 0.65f, 1f));
            DrawLane("权威锚点", frame.AuthorityPosition, new Color(1f, 0.66f, 0.26f), "未交付");
        }

        private void DrawActorProjection(LocalStateSyncReplay.Frame frame)
        {
            if (!_hasRecordedActorPose || _acceptance?.ActorModelId is not int modelId ||
                BattleScenarioEditorIntegration.CreateActorReplayPreview is not { } createPreview) return;

            var show = EditorGUILayout.ToggleLeft("3D 双投影对照（仅角色）", _showActorProjection);
            if (!show)
            {
                if (_showActorProjection) CloseActorProjection();
                return;
            }

            if (!_showActorProjection)
            {
                _showActorProjection = true;
                try { _actorProjection = createPreview(modelId); }
                catch (Exception error) { Debug.LogWarning($"[BattleScenario] 3D 演员投影初始化失败：{error.Message}"); }
            }

            if (_actorProjection == null)
            {
                EditorGUILayout.HelpBox("未找到录制对应的模型资源；仍可查看位置和 Tick。", MessageType.Info);
                return;
            }

            var authority = ToUnityPose(frame.AuthorityProjectionPose);
            var prediction = ToUnityPose(frame.PredictionPose);
            var predictionLabel = frame.Phase switch
            {
                LocalStateSyncReplay.FramePhase.Compare => "旧预测",
                LocalStateSyncReplay.FramePhase.Anchor or LocalStateSyncReplay.FramePhase.ReplayTick => "重演候选",
                _ => "预测",
            };
            EditorGUILayout.LabelField(frame.SharesAnchorPose ? "权威与候选重合 · 仅绘制原色候选" :
                $"权威（深灰）：{(authority.HasValue ? "已交付" : "无样本")}   {predictionLabel}（原色）：{(prediction.HasValue ? "有样本" : "无样本")}");
            var rect = GUILayoutUtility.GetRect(100f, 188f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                _actorProjection.Draw(rect, _recordedBounds, authority, prediction);
        }

        private static Pose? ToUnityPose(LocalStateSyncReplay.ActorPose? pose) => pose == null ? null :
            new Pose(new Vector3(pose.X, pose.Y, pose.Z), new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw));

        private void DrawAcceptanceHeader(LocalStateSyncReplay replay)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(_acceptanceLabel ?? _caseId, _caseId), EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(new GUIContent("切换回放…", "同一用例会定位到当前交付的检查点"), GUILayout.Width(92)))
                    {
                        _playing = false;
                        var selected = SelectAcceptance(out var source, out var error);
                        _importError = error;
                        if (selected != null)
                        {
                            SetAcceptance(selected, source);
                            GUIUtility.ExitGUI();
                        }
                    }
            }
            EditorGUILayout.LabelField($"变体：{replay.Variant} · {replay.TickRate} Tick/s");
            EditorGUILayout.LabelField(new GUIContent(Path.GetFileName(_acceptanceSource), _acceptanceSource), EditorStyles.miniLabel);
            if (_importError != null)
                EditorGUILayout.HelpBox(_importError, MessageType.Warning);
        }

        private void DrawDeliveryNavigation(LocalStateSyncReplay.Timeline timeline)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var presentation = _acceptance?.HasPresentationTrack == true;
                EditorGUILayout.LabelField(presentation ? "跳到 Tick" : "跳到交付", GUILayout.Width(64));
                var deliveryIndex = timeline.Frames[_index].DeliveryIndex;
                var selected = EditorGUILayout.Popup(deliveryIndex, _deliveryLabels, GUILayout.ExpandWidth(true));
                var delivery = timeline.Deliveries[selected];
                if (selected != deliveryIndex)
                    Seek(delivery.FirstFrame);

                if (!presentation)
                    using (new EditorGUI.DisabledScope(_index == delivery.CheckpointFrame))
                        if (GUILayout.Button(new GUIContent("看检查点", "跳过本次交付的回退与重演步骤"), GUILayout.Width(78)))
                            Seek(delivery.CheckpointFrame);
            }
        }

        private void DrawAcceptanceFrame(LocalStateSyncReplay.Frame frame)
        {
            var point = frame.Point;
            if (point.Presentation is { } sample)
            {
                DrawPresentation(point.DeliveryTick, sample);
                return;
            }
            DrawDetail("动作 / 交付 Tick", $"{point.Command} / {point.DeliveryTick}");
            switch (frame.Phase)
            {
                case LocalStateSyncReplay.FramePhase.Compare:
                    EditorGUILayout.LabelField(point.ComparisonTick is int tick
                        ? $"权威交付 · 对照同 Tick {tick} 预测" : "权威交付 · 对照上次预测", EditorStyles.boldLabel);
                    DrawLane("上次预测", frame.PreviousPrediction, new Color(0.30f, 0.65f, 1f));
                    DrawLane("权威锚点", point.Authority, new Color(1f, 0.66f, 0.26f));
                    break;
                case LocalStateSyncReplay.FramePhase.Anchor:
                    EditorGUILayout.LabelField("回退到权威起点", EditorStyles.boldLabel);
                    DrawDetail("权威生效 / 追赶目标 Tick", $"{point.AppliedTick} / {point.DeliveryTick}");
                    DrawLane("回退候选起点", frame.Position, new Color(0.30f, 0.65f, 1f));
                    break;
                case LocalStateSyncReplay.FramePhase.ReplayTick when frame.Step is { } step:
                    EditorGUILayout.LabelField($"重演 Tick {step.Tick}", EditorStyles.boldLabel);
                    DrawDetail("候选位置变化", $"{step.Before} → {step.After}");
                    DrawLane("上次预测", frame.PreviousPrediction, new Color(0.62f, 0.66f, 0.74f), "未观测");
                    DrawLane("权威锚点", point.Authority, new Color(1f, 0.66f, 0.26f));
                    DrawLane("重演位置", frame.Position, new Color(0.30f, 0.65f, 1f));
                    break;
                case LocalStateSyncReplay.FramePhase.Checkpoint:
                    var title = point.Prediction.HasValue && !point.Authority.HasValue
                        ? "本地预测 · 尚无权威样本" : point.Steps is { Length: > 0 } && point.Prediction.HasValue
                            ? "重演完成 · 交付后的新预测" : "交付检查点";
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    if (!point.Prediction.HasValue && frame.PreviousPrediction.HasValue)
                        DrawLane("上次预测", frame.PreviousPrediction, new Color(0.62f, 0.66f, 0.74f));
                    DrawCheckpoint(point);
                    break;
            }
        }

        private void DrawPresentation(int tick, LocalStateSyncReplay.PresentationObservation sample)
        {
            EditorGUILayout.LabelField($"Tick {tick} · {DescribePresentationPhase(sample.Phase)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("世界 Z 轨道（千分之一世界单位）", EditorStyles.miniLabel);
            DrawLane("权威观测", sample.Authority?.Z, new Color(0.42f, 0.47f, 0.50f), "无观测");
            var viewColor = sample.Phase == "predicted" ? new Color(0.98f, 0.68f, 0.23f) : new Color(0.30f, 0.76f, 0.72f);
            DrawLane("本地表现", sample.View?.Z, viewColor, "无实例");
            DrawDetail("权威位置 (XYZ)", FormatPresentationPoint(sample.Authority));
            DrawDetail("表现位置 (XYZ)", FormatPresentationPoint(sample.View));
            DrawDetail("活跃 / 创建 / 销毁", $"{sample.Active} / {sample.Created} / {sample.Destroyed}");
            DrawDetail("表现句柄", sample.Handle?.ToString() ?? "无");
            DrawDetail("对账指令", sample.CueCommand + (sample.Promoted ? " · 原实例晋升" : string.Empty));
            if (sample.RejectReason != "None") DrawDetail("忽略原因", sample.RejectReason);
            if (sample.DuplicateIgnored) DrawDetail("重复交付", "已忽略");
        }

        private static string DescribePresentationPhase(string phase) => phase switch
        {
            "predicted" => "本地预测",
            "confirmed" => "权威确认",
            "ended" => "自然退场",
            "rejected" => "权威拒绝",
            "late-prediction" => "迟到预测更新",
            _ => phase,
        };

        private static string FormatPresentationPoint(LocalStateSyncReplay.PresentationPoint? point) => point == null ? "无" :
            FormattableString.Invariant($"({point.X / 1000d:F3}, {point.Y / 1000d:F3}, {point.Z / 1000d:F3})");

        private void DrawCheckpoint(LocalStateSyncReplay.Checkpoint point)
        {
            if (point.Commit is { } commit)
                DrawLane("当前物化值", commit.MaterializedPosition, new Color(0.60f, 0.80f, 0.53f));
            else
                DrawLane("已发布位置", point.PublishedPosition, new Color(0.60f, 0.80f, 0.53f));
            DrawLane("权威锚点", point.Authority, new Color(1f, 0.66f, 0.26f));
            DrawLane("预测位置", point.Prediction, new Color(0.30f, 0.65f, 1f));

            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
            {
                _detailScroll = scroll.scrollPosition;
                var outcome = DescribeOutcome(point);
                if (outcome != null) EditorGUILayout.HelpBox(outcome, MessageType.Info);
                if (point.Commit is { } observation)
                {
                    DrawDetail("准入修订 / Tick", $"{observation.AdmittedRevision?.ToString() ?? "无"} / {observation.AdmittedTick?.ToString() ?? "无"}");
                    DrawDetail("物化修订 / 位置", $"{observation.MaterializedRevision?.ToString() ?? "无"} / {observation.MaterializedPosition?.ToString() ?? "无"}");
                    DrawDetail("本次尝试阶段", observation.Stages.Length == 0 ? "无" : string.Join(" → ", observation.Stages));
                    DrawDetail("等待全量", point.PredictionWaiting ? "是" : "否");
                }
                else
                {
                    DrawDetail("准入 / 校验", $"{point.Admission ?? "未执行"} / {point.Validation ?? "未执行"}");
                    DrawDetail("等待全量", $"流：{(point.StreamWaiting ? "是" : "否")} · 预测：{(point.PredictionWaiting ? "是" : "否")}");
                    DrawDetail("准入 / 基线 Tick", $"{point.AppliedTick} / {point.BaselineTick}");
                    DrawDetail("快照序号 / 导入次数", $"{point.Sequence} / {point.Imports}");
                }
                DrawDetail("对账", point.Reconciliation ?? "未执行");
                DrawDetail("待终结输入", point.Pending.Length == 0 ? "无" : string.Join("、", point.Pending));
                DrawDetail("实际重演 Tick", point.ReplayTicks.Length == 0 ? "无" : string.Join("、", point.ReplayTicks));
                DrawDetail("发布 / 对账修订", $"{point.PublishedRevision?.ToString() ?? "无"} / {point.ReconciledRevision}");
                DrawDetail("代际 / 终结序号", $"{point.Generation} / {point.TerminalSequence}");
            }
        }

        private static string? DescribeOutcome(LocalStateSyncReplay.Checkpoint point)
        {
            if (point.Reconciliation == "MissingTickCoverage") return "历史不完整：保留权威，本次未重演。";
            if (point.Reconciliation == "InvalidTerminalProof") return "缺少有效终结证明：恢复权威，不重演输入。";
            if (point.PredictionWaiting) return point.Authority.HasValue
                ? "等待新全量：保留已交付的权威锚点，暂不显示预测。"
                : "等待新全量：当前权威锚点和预测均已失效。";
            if (point.Admission == "IgnoredStale" || point.Reconciliation == "Ignored") return "旧交付被忽略，没有触发重演。";
            if (point.ReplayTicks.Length != 0 && point.Prediction.HasValue) return "重演完成，提交新预测。";
            return null;
        }

        private static void DrawDetail(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(122));
                EditorGUILayout.LabelField(value, EditorStyles.wordWrappedLabel);
            }
        }

        private void DrawLane(string label, int? position, Color color, string missing = "无")
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(73));
                var bounds = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
                var start = bounds.xMin + 7;
                var end = bounds.xMax - 7;
                EditorGUI.DrawRect(new Rect(start, bounds.center.y - 1, end - start, 2), new Color(0.5f, 0.5f, 0.5f, 0.55f));
                if (position is int value)
                {
                    var span = (double)_maxPosition - _minPosition;
                    var normalized = span > 0 ? (float)((value - (double)_minPosition) / span) : 0.5f;
                    var center = Mathf.Lerp(start, end, normalized);
                    EditorGUI.DrawRect(new Rect(center - 5, bounds.center.y - 6, 10, 12), color);
                }
                EditorGUILayout.LabelField(position?.ToString() ?? missing, GUILayout.Width(75));
            }
        }

        private void Seek(int target)
        {
            _index = target;
            _playing = false;
            Repaint();
        }

        private void IncludePosition(int? position)
        {
            if (position is not int value) return;
            _minPosition = Math.Min(_minPosition, value);
            _maxPosition = Math.Max(_maxPosition, value);
        }

        private static string Label(LocalCorrectionPreview.Phase stage) => stage switch
        {
            LocalCorrectionPreview.Phase.Start => "初始位置",
            LocalCorrectionPreview.Phase.Predicted => "本地预测",
            LocalCorrectionPreview.Phase.Match => "权威一致 · 不回滚",
            LocalCorrectionPreview.Phase.Mismatch => "发现预测偏差",
            LocalCorrectionPreview.Phase.Restored => "恢复权威位置",
            LocalCorrectionPreview.Phase.Replayed => "重演后续输入",
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };
    }
}
#endif
