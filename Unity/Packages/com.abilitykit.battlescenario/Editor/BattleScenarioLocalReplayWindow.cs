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
        private bool _attemptedActorProjection;
        private bool _directPreview;
        private bool _hasRecordedActorPose;
        private IActorReplayPreview? _actorProjection;
        private Bounds _recordedBounds;
        private int FrameCount => _acceptanceTimeline?.Frames.Count ?? _frames.Count;
        private double StepInterval
        {
            get
            {
                if (!_directPreview || _acceptanceTimeline == null) return SecondsPerStep / Speeds[_speedIndex];
                var frame = _acceptanceTimeline.Frames[_index];
                var highlight = frame.Phase == LocalStateSyncReplay.FramePhase.BeforePublication ||
                    frame.Point.Steps is { Length: > 0 } || _acceptance?.SkillDelivery?.DeliveryTick == frame.Point.DeliveryTick ||
                    frame.Point.Presentation?.Phase == "rejected" ||
                    (frame.Point.HasUndeliveredAuthorityReference && _index + 1 < FrameCount &&
                        _acceptanceTimeline.Frames[_index + 1].Point.Steps is { Length: > 0 }) ||
                    (FrameCount == 2 && frame.PredictionPose != null);
                return (highlight ? 0.85 : Math.Max(0.04, Math.Min(0.22, 2.2 / FrameCount))) / Speeds[_speedIndex];
            }
        }

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
            window._directPreview = false;
            window.ResetTimeline(caseId);
            foreach (var frame in frames)
            {
                window.IncludePosition(frame.ViewPosition);
                if (frame.AuthorityPosition is int authority) window.IncludePosition(authority);
            }
            window.StartPlaying();
            window.Show();
        }

        internal static void OpenAcceptance(LocalStateSyncReplay replay, string source, string? label = null, bool autoPreview = false)
        {
            var window = GetWindow<BattleScenarioLocalReplayWindow>();
            window.SetAcceptance(replay, source, label, autoPreview);
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

        internal void SetAcceptance(LocalStateSyncReplay replay, string source, string? label = null, bool autoPreview = false)
        {
            var timeline = replay.BuildTimeline(inspectSteps: !autoPreview);
            var matchingIndex = autoPreview || _acceptanceTimeline == null ? null : timeline.FindMatchingCheckpoint(_acceptanceTimeline, _index);
            titleContent = new GUIContent("状态同步验收回放");
            minSize = new Vector2(530, 420);
            _acceptance = replay;
            _acceptanceTimeline = timeline;
            _acceptanceSource = source;
            _acceptanceLabel = label;
            _importError = null;
            _frames = Array.Empty<LocalCorrectionPreview.Frame>();
            _directPreview = autoPreview;
            _deliveryLabels = new string[replay.Checkpoints.Length];
            ResetTimeline(replay.CaseId);
            for (var index = 0; index < replay.Checkpoints.Length; index++)
            {
                var point = replay.Checkpoints[index];
                var replaySteps = point.ReplayTicks.Length == 0 ? string.Empty : $" · 重演 {point.ReplayTicks.Length} Tick";
                _deliveryLabels[index] = $"{index + 1}. Tick {point.DeliveryTick} · " +
                    (replay.SkillDelivery is { } skill ? DescribeSkillTick(skill, point.DeliveryTick) :
                        point.Presentation is { } sample ? DescribePresentationPhase(sample.Phase) : point.Command + replaySteps);
                IncludePosition(point.Presentation?.Authority?.Z);
                IncludePosition(point.Presentation?.View?.Z);
                IncludePosition(point.PublishedPosition);
                IncludePosition(point.Authority);
                IncludePosition(point.Prediction);
                IncludePosition(point.Commit?.MaterializedPosition);
            }
            var hasBounds = false;
            foreach (var frame in (autoPreview ? replay.BuildTimeline() : timeline).Frames)
            {
                IncludePosition(frame.Position);
                IncludePosition(frame.FlightAuthority?.Z);
                IncludePosition(frame.FlightPrediction?.Z);
                IncludePoint(ToFlightPosition(frame.FlightAuthority));
                IncludePoint(ToFlightPosition(frame.FlightPrediction));
                IncludePose(frame.Point.AuthorityPose);
                IncludePose(frame.PredictionPose);
            }

            void IncludePose(LocalStateSyncReplay.ActorPose? pose)
            {
                if (ToUnityPose(pose) is not Pose observed) return;
                IncludePoint(observed.position);
                _hasRecordedActorPose = true;
            }
            Seek(matchingIndex ?? 0);
            _showActorProjection = autoPreview && _hasRecordedActorPose;
            if (autoPreview && !EditorApplication.isPlayingOrWillChangePlaymode) StartPlaying();

            void IncludePoint(Vector3? value)
            {
                if (value is not Vector3 point) return;
                if (hasBounds) _recordedBounds.Encapsulate(point);
                else _recordedBounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
        }

        internal bool IsAutoPlayingPreview => _directPreview && _playing;

        private void SetPreviewMode(bool direct)
        {
            var next = _acceptance!.BuildTimeline(inspectSteps: !direct);
            var matched = next.FindMatchingCheckpoint(_acceptanceTimeline!, _index);
            _acceptanceTimeline = next;
            _directPreview = direct;
            Seek(matched ?? 0);
            if (!direct) return;
            _showActorProjection = _hasRecordedActorPose;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) StartPlaying();
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
            _attemptedActorProjection = false;
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
                if (_directPreview && _acceptanceTimeline is { } preview)
                {
                    DrawDirectPreview(preview);
                    return;
                }
                if (GUILayout.Button("直接预览", EditorStyles.miniButton))
                {
                    SetPreviewMode(true);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(replay.PositionLabel))
                    EditorGUILayout.LabelField("坐标刻度", replay.PositionLabel);
                EditorGUILayout.HelpBox(replay.SkillDelivery?.Fallback != null
                    ? "离线施法降级录制。最终只采用已提交的权威截面；旧预测仅用于对照，不代表当前状态。"
                    : replay.SkillDelivery != null
                    ? "离线技能逻辑录制。权威参考来自独立世界；重演候选追平后才切换发布轨道。不会执行技能或修改草稿。"
                    : replay.HasPresentationTrack
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

        private void DrawDirectPreview(LocalStateSyncReplay.Timeline timeline)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("重播")) { Seek(0); StartPlaying(); }
                    if (GUILayout.Button(_playing ? "暂停" : "播放"))
                    {
                        if (_playing) _playing = false;
                        else StartPlaying();
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("逐 Tick 检查")) { SetPreviewMode(false); return; }
            }

            var frame = timeline.Frames[_index];
            if (frame.Point.Presentation is { } sample)
            {
                DrawProjectileLanes(frame.Point.DeliveryTick, sample);
                return;
            }
            var correction = _acceptance?.SkillDelivery is { Fallback: null, Steps: { Length: > 0 } } skill &&
                skill.DeliveryTick == frame.Point.DeliveryTick || frame.Point.Steps is { Length: > 0 };
            var matchingLanding = _index > 0 && frame.Point.Reconciliation == "AuthoritativeOnly" &&
                !frame.Point.PredictionWaiting && frame.AuthorityProjectionPose is { } authority &&
                timeline.Frames[_index - 1].PredictionPose is { } prior &&
                timeline.Frames[_index - 1].Point.DeliveryTick == frame.Point.AppliedTick && authority.Matches(prior);
            var stage = frame.Phase == LocalStateSyncReplay.FramePhase.BeforePublication ? "纠偏前 · 旧预测" :
                correction ? "追平完成 · 新预测已发布" : matchingLanding ? "权威落点吻合 · 无需重演" :
                frame.Point.HasUndeliveredAuthorityReference ? "双世界对照 · 快照未交付" :
                frame.AuthorityProjectionPose == null && frame.PredictionPose != null ? "本地先显示预测" : "本地预览";
            EditorGUILayout.LabelField(stage, $"Tick {frame.Point.DeliveryTick}");
            if (_acceptance?.HasStepPositions == true)
                EditorGUILayout.LabelField(frame.Point.Command, EditorStyles.wordWrappedLabel);
            var rendered = DrawActorProjection(frame);
            if (!rendered) DrawAcceptanceFrame(frame);
            else if (_acceptance?.SkillDelivery?.FormalFlight == true)
                EditorGUILayout.LabelField("弹体标记：权威深灰 / 预测橙色", frame.Point.Command);
        }

        private bool DrawActorProjection(LocalStateSyncReplay.Frame frame)
        {
            if (!_hasRecordedActorPose || _acceptance?.ActorModelId is not int modelId ||
                BattleScenarioEditorIntegration.CreateActorReplayPreview is not { } createPreview) return false;

            var show = _directPreview || EditorGUILayout.ToggleLeft("3D 双投影对照（仅角色）", _showActorProjection);
            if (!show)
            {
                if (_showActorProjection) CloseActorProjection();
                return false;
            }

            _showActorProjection = true;
            if (!_attemptedActorProjection)
            {
                _attemptedActorProjection = true;
                try { _actorProjection = createPreview(modelId); }
                catch (Exception error) { Debug.LogWarning($"[BattleScenario] 3D 演员投影初始化失败：{error.Message}"); }
            }

            if (_actorProjection == null)
            {
                EditorGUILayout.HelpBox("未找到录制对应的模型资源；仍可查看位置和 Tick。", MessageType.Info);
                return false;
            }

            var authority = ToUnityPose(frame.AuthorityProjectionPose);
            var prediction = ToUnityPose(frame.PredictionPose);
            var overlapping = frame.SharesAnchorPose || (authority is Pose authorityPose && prediction is Pose predictionPose &&
                (authorityPose.position - predictionPose.position).sqrMagnitude < 0.000001f);
            var predictionLabel = frame.Phase switch
            {
                LocalStateSyncReplay.FramePhase.Compare or LocalStateSyncReplay.FramePhase.BeforePublication => "旧预测",
                LocalStateSyncReplay.FramePhase.Anchor or LocalStateSyncReplay.FramePhase.ReplayTick => "重演候选",
                _ => "预测",
            };
            var authorityLabel = frame.Point.HasUndeliveredAuthorityReference ? "参考，未交付" : "已交付";
            EditorGUILayout.LabelField(overlapping ? "两路位置重合 · 仅绘制原色" :
                $"权威（深灰）：{(authority.HasValue ? authorityLabel : "无样本")}   {predictionLabel}（原色）：{(prediction.HasValue ? "有样本" : "无样本")}");
            var rect = GUILayoutUtility.GetRect(100f, _directPreview ? 285f : 188f,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(_directPreview));
            if (Event.current.type == EventType.Repaint)
            {
                if (_actorProjection is IFlightReplayPreview flight)
                    flight.SetFlightPositions(ToFlightPosition(frame.FlightAuthority), ToFlightPosition(frame.FlightPrediction));
                _actorProjection.Draw(rect, _recordedBounds, overlapping ? null : authority, prediction);
            }
            return true;
        }

        private static Vector3? ToFlightPosition(LocalStateSyncReplay.FlightSample? sample) => sample == null ? null :
            new Vector3(sample.X, sample.Y, sample.Z) * 0.001f;

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
                var skill = _acceptance?.SkillDelivery;
                EditorGUILayout.LabelField(presentation || skill != null ? "跳到 Tick" : "跳到交付", GUILayout.Width(64));
                var deliveryIndex = timeline.Frames[_index].DeliveryIndex;
                var selected = EditorGUILayout.Popup(deliveryIndex, _deliveryLabels, GUILayout.ExpandWidth(true));
                var delivery = timeline.Deliveries[selected];
                if (selected != deliveryIndex)
                    Seek(delivery.FirstFrame);

                if (skill != null && selected != skill.DeliveryTick - timeline.Frames[0].Point.DeliveryTick)
                {
                    if (GUILayout.Button(skill.Fallback != null ? "看降级" : "看和解", GUILayout.Width(65)))
                        Seek(timeline.Deliveries[skill.DeliveryTick - timeline.Frames[0].Point.DeliveryTick].FirstFrame);
                }
                else if (!presentation)
                    using (new EditorGUI.DisabledScope(_index == delivery.CheckpointFrame))
                        if (GUILayout.Button(new GUIContent("看检查点", "跳过本次交付的回退与重演步骤"), GUILayout.Width(78)))
                            Seek(delivery.CheckpointFrame);
            }
        }

        private void DrawAcceptanceFrame(LocalStateSyncReplay.Frame frame)
        {
            var point = frame.Point;
            if (_acceptance?.SkillDelivery is { } skill)
            {
                if (skill.FormalFlight) DrawFlightProjection(frame);
                DrawSkillFrame(frame, skill);
                return;
            }
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
                    if (point.HasUndeliveredAuthorityReference)
                    {
                        EditorGUILayout.LabelField("双世界观测 · 未交付快照", EditorStyles.boldLabel);
                        DrawLane("独立权威参考", point.Authority, new Color(1f, 0.66f, 0.26f));
                        DrawLane("预测位置", point.Prediction, new Color(0.30f, 0.65f, 1f));
                        break;
                    }
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

        private static string DescribeSkillTick(LocalStateSyncReplay.SkillDeliveryObservation skill, int tick)
        {
            if (tick == skill.FirstInputTick) return skill.FormalWindup ? "施法按键 · 初次预测" : $"{skill.PrimaryLabel} · 初次预测";
            if (tick == skill.AnchorTick) return "后续权威截面对应 Tick";
            if (skill.FormalMovement && tick == skill.InputTick) return "移动转向 · 待确认输入";
            if (!skill.FormalWindup && tick == skill.InputTick) return $"{skill.SecondaryLabel} · 待确认输入";
            if (tick == skill.DeliveryTick) return skill.Fallback != null ? "采用权威截面（不重演）" : "收到权威结果";
            return tick < skill.DeliveryTick ? "本地预测" : "发布后观测";
        }

        private static void DrawSkillFrame(LocalStateSyncReplay.Frame frame, LocalStateSyncReplay.SkillDeliveryObservation skill)
        {
            var displayPoint = frame.SkillReference ?? frame.Point;
            var observation = displayPoint.Skill!;
            var corrected = skill.Steps.Length > 0;
            var matches = observation.Authority.Matches(observation.OldPrediction);
            var fallback = frame.Point.DeliveryTick == skill.DeliveryTick ? skill.Fallback : null;
            var title = frame.Phase switch
            {
                LocalStateSyncReplay.FramePhase.Compare => $"同 Tick {skill.AnchorTick} 对照 · {(matches ? "观测匹配" : "观测不匹配")}",
                LocalStateSyncReplay.FramePhase.Anchor => $"回到权威基线 · Tick {skill.AnchorTick}",
                LocalStateSyncReplay.FramePhase.ReplayTick => $"重演 Tick {displayPoint.DeliveryTick} · 候选未发布",
                _ when fallback != null => fallback.Reason switch
                {
                    LocalStateSyncReplay.SkillReplayCase.HistoryGap => "历史缺口 · 不重演",
                    LocalStateSyncReplay.SkillReplayCase.MissingProof => "缺终结证明 · 不重演",
                    _ => "会话换代 · 忽略旧代交付",
                },
                _ when frame.Point.DeliveryTick == skill.DeliveryTick => corrected ? "追平并发布" : "权威确认 · 保留原预测",
                _ when skill.CastEconomy && frame.Point.DeliveryTick > skill.DeliveryTick => "接管后的预测 Tick",
                _ => DescribeSkillTick(skill, displayPoint.DeliveryTick),
            };
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawDetail("观测 / 交付 Tick", $"{displayPoint.DeliveryTick} / {skill.DeliveryTick}");
            if (skill.FormalBattleCandidate)
            {
                DrawDetail("实际提交的权威 Tick", frame.Point.AppliedTick.ToString());
                if (frame.Phase == LocalStateSyncReplay.FramePhase.Compare && matches)
                    DrawDetail("恢复策略", "法力和冷却一致仍构建候选；不据此复用旧世界");
            }
            if (fallback != null)
            {
                DrawDetail("实际采用的权威 Tick", $"{frame.Point.AppliedTick}（会话第 {frame.Point.Generation} 代）");
                DrawDetail("重演输入 / 保留待确认输入", $"无 / {fallback.RetainedInputCount} 条");
                if (fallback.MissingHistoryTick is int missingTick)
                    DrawDetail("缺失的封存 Tick", missingTick.ToString());
                if (skill.TerminalThroughSequence > 0)
                    DrawDetail("已终结前缀", $"#{skill.TerminalThroughSequence}");
            }
            else if (frame.Point.DeliveryTick == skill.DeliveryTick)
                DrawDetail("终结前缀 / 待确认", skill.FormalWindup && !skill.FormalMovement ? $"#{skill.TerminalThroughSequence} / 无" :
                    $"#{skill.TerminalThroughSequence} / #{skill.PendingSequence}");
            if (!skill.FormalWindup && displayPoint.DeliveryTick == skill.InputTick)
                DrawDetail("旧预测的待确认输入", FormatSkillInputOutcome(skill.OldInputRejectCode, "已在旧预测中执行"));
            if (frame.SkillStep is { } inputStep && inputStep.Tick == skill.InputTick)
            {
                DrawDetail("候选输入", $"按原 Tick 重演 #{skill.ReplayedSequence}（{(skill.FormalMovement ? "移动转向" : skill.SecondaryLabel)}）");
                if (skill.CastEconomy && !skill.FormalMovement)
                    DrawDetail("候选输入结果", FormatSkillInputOutcome(inputStep.InputRejection, "通过冷却检查并执行"));
            }
            else if (skill.FormalBattleCandidate && frame.SkillStep != null)
                DrawDetail("候选输入", "空 Tick · 只推进战斗世界");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("状态轨道", EditorStyles.miniBoldLabel, GUILayout.Width(128));
                if (skill.CastProgressOnly)
                    GUILayout.Label("前摇已推进", EditorStyles.miniBoldLabel, GUILayout.Width(85));
                else if (skill.CastEconomy)
                {
                    GUILayout.Label("法力", EditorStyles.miniBoldLabel, GUILayout.Width(65));
                    GUILayout.Label(skill.FormalBattleCandidate ? skill.PrimaryLabel : "冷却结束（ms）", EditorStyles.miniBoldLabel, GUILayout.Width(106));
                    GUILayout.Label(skill.FormalBattleCandidate ? skill.SecondaryLabel : "前摇已推进", EditorStyles.miniBoldLabel, GUILayout.Width(85));
                    if (skill.FormalMovement) GUILayout.Label("位置（X, Z）", EditorStyles.miniBoldLabel);
                }
                else
                {
                    GUILayout.Label("魔力", EditorStyles.miniBoldLabel, GUILayout.Width(65));
                    GUILayout.Label("目标生命", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    GUILayout.Label(skill.PrimaryLabel, EditorStyles.miniBoldLabel, GUILayout.Width(94));
                    GUILayout.Label(skill.SecondaryLabel, EditorStyles.miniBoldLabel, GUILayout.Width(94));
                }
            }
            if (fallback != null)
                DrawSkillRow("已采用的权威截面", observation.Published, skill);
            else
                DrawSkillRow(frame.Phase is LocalStateSyncReplay.FramePhase.Anchor ? "权威基线" : "权威参考", observation.Authority, skill);
            if (frame.Phase != LocalStateSyncReplay.FramePhase.Anchor && (!skill.CastEconomy || displayPoint.DeliveryTick <= skill.DeliveryTick))
                DrawSkillRow("旧预测", observation.OldPrediction, skill);
            if (frame.SkillStep is { } step)
                DrawSkillRow("重演候选（未发布）", step.State, skill);
            else if (frame.Phase == LocalStateSyncReplay.FramePhase.Checkpoint && fallback == null)
                DrawSkillRow("实际发布", observation.Published, skill);
            EditorGUILayout.LabelField(skill.FormalMovement ? "移动方向在空 Tick 保持；权威投影不随候选追平推进。" :
                skill.FormalWindup ? "已终结的按键不重发；— 表示施法已结束或尚未开始。" :
                skill.FormalBattleCandidate ? "冷却为绝对结束时刻（ms）；此样例不恢复伤害或在途效果。" :
                skill.CastProgressOnly ? "— 表示没有进行中的施法；此样例不观测战斗数值。" :
                skill.CastEconomy ? "冷却是绝对结束时刻，不是剩余时间；— 表示没有冷却或进行中的施法。" :
                "冷却列：世界时钟的结束时刻（ms）；— 表示未记录冷却。", EditorStyles.miniLabel);
        }

        private static string FormatSkillInputOutcome(string? rejection, string acceptedLabel) => rejection == null
            ? acceptedLabel : $"被拦截：{rejection}";

        private static void DrawSkillRow(string label, LocalStateSyncReplay.SkillState state, LocalStateSyncReplay.SkillDeliveryObservation skill)
        {
            const decimal rawPerUnit = 4294967296m;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(128));
                if (skill.CastEconomy)
                {
                    GUILayout.Label($"{state.ManaRaw / rawPerUnit:0.##}", GUILayout.Width(65));
                    GUILayout.Label(FormatCooldown(state.PrimaryCooldownEndMs), GUILayout.Width(106));
                    if (skill.FormalBattleCandidate && !skill.FormalWindup && !skill.FormalMovement)
                    {
                        GUILayout.Label(FormatCooldown(state.SecondaryCooldownEndMs), GUILayout.Width(85));
                        return;
                    }
                }
                if (skill.CastProgressOnly || skill.CastEconomy)
                {
                    GUILayout.Label(state.CastElapsedMs is int elapsed ? $"{elapsed} ms" : "—", GUILayout.Width(85));
                    if (skill.FormalMovement) GUILayout.Label($"({state.PositionX:0.###}, {state.PositionZ:0.###})");
                    return;
                }
                GUILayout.Label($"{state.ManaRaw / rawPerUnit:0.##}", GUILayout.Width(65));
                GUILayout.Label($"{state.TargetHealthRaw / rawPerUnit:0.##}", GUILayout.Width(80));
                GUILayout.Label(FormatCooldown(state.PrimaryCooldownEndMs), GUILayout.Width(94));
                GUILayout.Label(FormatCooldown(state.SecondaryCooldownEndMs), GUILayout.Width(94));
            }

            static string FormatCooldown(long endMs) => endMs == 0 ? "—" : endMs.ToString();
        }

        private void DrawPresentation(int tick, LocalStateSyncReplay.PresentationObservation sample)
        {
            DrawProjectileLanes(tick, sample);
            DrawDetail("权威位置 (XYZ)", FormatPresentationPoint(sample.Authority));
            DrawDetail("表现位置 (XYZ)", FormatPresentationPoint(sample.View));
            DrawDetail("活跃 / 创建 / 销毁", $"{sample.Active} / {sample.Created} / {sample.Destroyed}");
            DrawDetail("表现句柄", sample.Handle?.ToString() ?? "无");
            DrawDetail("对账指令", sample.CueCommand + (sample.Promoted ? " · 原实例晋升" : string.Empty));
            if (sample.RejectReason != "None") DrawDetail("忽略原因", sample.RejectReason);
            if (sample.DuplicateIgnored) DrawDetail("重复交付", "已忽略");
        }

        private void DrawProjectileLanes(int tick, LocalStateSyncReplay.PresentationObservation sample)
        {
            EditorGUILayout.LabelField($"Tick {tick} · {DescribePresentationPhase(sample.Phase)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("弹体表现位置 · 世界 Z 轴（千分之一世界单位）", EditorStyles.miniLabel);
            DrawLane("权威观测", sample.Authority?.Z, new Color(0.42f, 0.47f, 0.50f), "无观测");
            var viewColor = sample.Phase == "predicted" ? new Color(0.98f, 0.68f, 0.23f) : new Color(0.30f, 0.76f, 0.72f);
            var absent = sample.Phase switch { "rejected" => "已移除", "late-prediction" => "未复活", _ => "无实例" };
            DrawLane("本地表现", sample.View?.Z, viewColor, absent);
        }

        private void DrawFlightProjection(LocalStateSyncReplay.Frame frame)
        {
            var title = frame.Phase == LocalStateSyncReplay.FramePhase.BeforePublication ? "交付前 · 旧预测" : frame.Point.Command;
            EditorGUILayout.LabelField($"Tick {frame.Point.DeliveryTick} · {title}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("逻辑弹体双投影 · 世界 Z 轴（千分之一世界单位）", EditorStyles.miniLabel);
            DrawLane("已提交权威", frame.FlightAuthority?.Z, new Color(0.42f, 0.47f, 0.50f));
            var label = frame.Phase switch
            {
                LocalStateSyncReplay.FramePhase.ReplayTick => "未发布候选",
                LocalStateSyncReplay.FramePhase.Anchor => "恢复基线",
                LocalStateSyncReplay.FramePhase.Compare => "旧预测（同 Tick）",
                _ => "本地预测",
            };
            DrawLane(label, frame.FlightPrediction?.Z, new Color(0.30f, 0.76f, 0.72f));
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
