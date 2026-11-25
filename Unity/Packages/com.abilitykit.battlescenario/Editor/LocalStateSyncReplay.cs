#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.BattleScenario.Editor
{
    /// <summary>
    /// 本地状态同步验收检查点的回放格式。仅供编辑器及同源测试使用，
    /// 不依赖状态同步实现，不执行场景命令，也不是快照传输协议。
    /// </summary>
    internal sealed class LocalStateSyncReplay
    {
        private const string FormatId = "abilitykit.local-statesync-replay";
        private const int CheckpointOnlyVersion = 1;
        private const int CurrentVersion = 2;
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MaxDepth = 12,
        };

        [JsonProperty(Required = Required.Always)] internal string Format = FormatId;
        [JsonProperty(Required = Required.Always)] internal int Version = CurrentVersion;
        [JsonProperty(Required = Required.Always)] internal string CaseId = string.Empty;
        [JsonProperty(Required = Required.Always)] internal string Variant = string.Empty;
        [JsonProperty(Required = Required.Always)] internal int TickRate;
        /// <summary>
        /// 可选的绘图轴和刻度说明；不参与对账或位置推导。
        /// </summary>
        [JsonProperty("positionLabel", NullValueHandling = NullValueHandling.Ignore)] internal string? PositionLabel = null;
        [JsonProperty("actorModelId", NullValueHandling = NullValueHandling.Ignore)] internal int? ActorModelId = null;
        [JsonProperty(Required = Required.Always)] internal Checkpoint[] Checkpoints = Array.Empty<Checkpoint>();

        internal bool HasStepPositions => Version == CurrentVersion;
        internal bool HasPresentationTrack => Checkpoints.Length > 0 && Checkpoints[0]?.Presentation != null;

        internal string Serialize()
        {
            Validate();
            return JsonConvert.SerializeObject(this, Settings);
        }

        internal static LocalStateSyncReplay Parse(string json)
        {
            var replay = JsonConvert.DeserializeObject<LocalStateSyncReplay>(json, Settings)
                         ?? throw new InvalidDataException("本地状态同步回放为空。");
            replay.Validate();
            return replay;
        }

        internal Timeline BuildTimeline()
        {
            var frames = new List<Frame>(Checkpoints.Length);
            var deliveries = new DeliveryRange[Checkpoints.Length];
            for (var deliveryIndex = 0; deliveryIndex < Checkpoints.Length; deliveryIndex++)
            {
                var point = Checkpoints[deliveryIndex];
                var firstFrame = frames.Count;
                var prior = deliveryIndex > 0 ? Checkpoints[deliveryIndex - 1] : null;
                if (prior?.Generation != point.Generation) prior = null;
                var previousPrediction = prior?.Prediction;
                if (point.Steps is { Length: > 0 } steps)
                {
                    var comparison = point.ComparisonTick.HasValue ? FindComparison(deliveryIndex, point) : prior;
                    if (comparison?.Prediction is int comparedPosition && point.Authority.HasValue)
                        frames.Add(new Frame(point, deliveryIndex, FramePhase.Compare, previousPrediction: comparedPosition,
                            previousPose: comparison.PredictionPose));
                    frames.Add(new Frame(point, deliveryIndex, FramePhase.Anchor, steps[0]));
                    foreach (var step in steps)
                        frames.Add(new Frame(point, deliveryIndex, FramePhase.ReplayTick, step, previousPrediction));
                }
                deliveries[deliveryIndex] = new DeliveryRange(firstFrame, frames.Count);
                frames.Add(new Frame(point, deliveryIndex, FramePhase.Checkpoint, previousPrediction: previousPrediction));
            }
            return new Timeline(CaseId, frames, deliveries);
        }

        internal void Validate()
        {
            if (Format != FormatId || (Version != CheckpointOnlyVersion && Version != CurrentVersion))
                throw new InvalidDataException("不支持的本地状态同步回放格式或版本。");
            if (string.IsNullOrWhiteSpace(CaseId) || string.IsNullOrWhiteSpace(Variant) || TickRate <= 0 || Checkpoints.Length == 0)
                throw new InvalidDataException("本地状态同步回放缺少用例、变体、Tick 频率或检查点。");
            if (ActorModelId.HasValue && (Version != CurrentVersion || ActorModelId <= 0))
                throw new InvalidDataException("演员投影需要有效模型标识和逐 Tick 回放格式。");

            var previousTick = 0;
            for (var index = 0; index < Checkpoints.Length; index++)
            {
                var point = Checkpoints[index];
                if (point == null || string.IsNullOrWhiteSpace(point.Command) || point.DeliveryTick < previousTick)
                    throw new InvalidDataException("本地状态同步回放存在空检查点、无名命令或倒序检查点。");
                ValidateSteps(point);
                ValidatePresentation(point, index == 0 ? null : Checkpoints[index - 1]);
                if (point.ComparisonTick.HasValue && (point.ComparisonTick != point.AppliedTick ||
                    point.Steps is not { Length: > 0 } || !point.Authority.HasValue || FindComparison(index, point) == null))
                    throw new InvalidDataException("同 Tick 对照缺少同代预测或权威样本。");
                ValidatePose(point.AuthorityPose, point.Authority);
                ValidatePose(point.PredictionPose, point.Prediction);
                if (point.Steps != null)
                    foreach (var step in point.Steps) ValidatePose(step.AfterPose, step.After);
                previousTick = point.DeliveryTick;
            }
        }

        private void ValidatePresentation(Checkpoint point, Checkpoint? prior)
        {
            var sample = point.Presentation;
            if (HasPresentationTrack != (sample != null))
                throw new InvalidDataException("表现轨道需要为每个 Tick 提供独立观测。");
            if (sample == null) return;
            if (Version != CurrentVersion || ActorModelId.HasValue || point.ReplayTicks.Length != 0 ||
                (prior != null && (long)point.DeliveryTick != (long)prior.DeliveryTick + 1) ||
                string.IsNullOrWhiteSpace(sample.Phase) || string.IsNullOrWhiteSpace(sample.CueCommand) ||
                string.IsNullOrWhiteSpace(sample.RejectReason) ||
                sample.Active < 0 || sample.Active > 1 || sample.Created < 0 || sample.Destroyed < 0 ||
                sample.Created - sample.Destroyed != sample.Active ||
                (sample.Active == 1) != (sample.View != null) || (sample.Active == 1) != (sample.Handle is > 0) ||
                (sample.Active == 0 && sample.Handle.HasValue))
                throw new InvalidDataException("表现轨道的 Tick、位置或实例计数无效。");
        }

        private Checkpoint? FindComparison(int index, Checkpoint point)
        {
            for (var prior = index - 1; prior >= 0; prior--)
            {
                var sample = Checkpoints[prior];
                if (sample.Generation != point.Generation || sample.DeliveryTick < point.ComparisonTick) break;
                if (sample.DeliveryTick == point.ComparisonTick && sample.Prediction.HasValue) return sample;
            }
            return null;
        }

        private void ValidatePose(ActorPose? pose, int? plottedPosition)
        {
            if (pose == null) return;
            if (!ActorModelId.HasValue || !plottedPosition.HasValue || !pose.IsValid ||
                Math.Round(pose.Z * 1000f) != plottedPosition.Value)
                throw new InvalidDataException("演员姿态无效或与录制的 Z 轴观测不一致。");
        }

        /// <summary>
        /// 实际观测的世界空间位置与四元数；缺失时不合成姿态。
        /// </summary>
        internal sealed class ActorPose
        {
            [JsonProperty(Required = Required.Always)] internal float X { get; set; }
            [JsonProperty(Required = Required.Always)] internal float Y { get; set; }
            [JsonProperty(Required = Required.Always)] internal float Z { get; set; }
            [JsonProperty(Required = Required.Always)] internal float Qx { get; set; }
            [JsonProperty(Required = Required.Always)] internal float Qy { get; set; }
            [JsonProperty(Required = Required.Always)] internal float Qz { get; set; }
            [JsonProperty(Required = Required.Always)] internal float Qw { get; set; }

            internal bool IsValid => Finite(X) && Finite(Y) && Finite(Z) &&
                Math.Abs(Qx * Qx + Qy * Qy + Qz * Qz + Qw * Qw - 1f) < 0.01f;

            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ValidateSteps(Checkpoint point)
        {
            if (Version == CheckpointOnlyVersion)
            {
                if (point.Steps != null)
                    throw new InvalidDataException("旧版检查点不包含逐 Tick 重演位置。");
                return;
            }

            var steps = point.Steps;
            if (steps == null || steps.Length != point.ReplayTicks.Length)
                throw new InvalidDataException("逐 Tick 重演位置与已观测的重演 Tick 数量不一致。");

            var previousTick = -1;
            var previousPosition = point.Authority;
            for (var index = 0; index < steps.Length; index++)
            {
                var step = steps[index];
                if (step == null || step.Tick != point.ReplayTicks[index] || step.Tick <= previousTick ||
                    step.Tick > point.DeliveryTick || (previousPosition.HasValue && previousPosition.Value != step.Before))
                    throw new InvalidDataException("逐 Tick 重演的时间或位置不连续。");
                previousTick = step.Tick;
                previousPosition = step.After;
            }
            if (steps.Length > 0 && point.Prediction.HasValue && point.Prediction != previousPosition)
                throw new InvalidDataException("最后的重演位置与检查点的预测位置不一致。");
        }

        /// <summary>
        /// 一次交付或场景动作后的观测值。只有实际观测值；空值不推导为零或上一帧。
        /// </summary>
        internal sealed class Checkpoint
        {
            [JsonProperty(Required = Required.Always)] internal string Command = string.Empty;
            [JsonProperty(Required = Required.Always)] internal int DeliveryTick;
            [JsonProperty(Required = Required.Always)] internal int Generation;
            [JsonProperty(Required = Required.AllowNull)] internal string? Admission;
            [JsonProperty(Required = Required.AllowNull)] internal string? Validation;
            [JsonProperty(Required = Required.AllowNull)] internal string? Reconciliation;
            [JsonProperty(Required = Required.Always)] internal long Sequence;
            [JsonProperty(Required = Required.Always)] internal int AppliedTick;
            [JsonProperty(Required = Required.Always)] internal int BaselineTick;
            [JsonProperty(Required = Required.AllowNull)] internal long? PublishedRevision;
            [JsonProperty(Required = Required.Always)] internal long ReconciledRevision;
            [JsonProperty(Required = Required.Always)] internal long TerminalSequence;
            [JsonProperty(Required = Required.Always)] internal bool StreamWaiting;
            [JsonProperty(Required = Required.Always)] internal bool PredictionWaiting;
            [JsonProperty(Required = Required.Always)] internal int Imports;
            [JsonProperty(Required = Required.AllowNull)] internal int? PublishedPosition;
            [JsonProperty(Required = Required.AllowNull)] internal int? Authority;
            [JsonProperty(Required = Required.AllowNull)] internal int? Prediction;
            /// <summary>
            /// 明确指定用于对照的同代预测观测 Tick；旧录制仍显示交付前最后一次观测。
            /// </summary>
            [JsonProperty("comparisonTick", NullValueHandling = NullValueHandling.Ignore)] internal int? ComparisonTick = null;
            [JsonProperty("authorityPose", NullValueHandling = NullValueHandling.Ignore)] internal ActorPose? AuthorityPose = null;
            [JsonProperty("predictionPose", NullValueHandling = NullValueHandling.Ignore)] internal ActorPose? PredictionPose = null;
            [JsonProperty(Required = Required.Always)] internal long[] Pending = Array.Empty<long>();
            [JsonProperty(Required = Required.Always)] internal int[] ReplayTicks = Array.Empty<int>();
            [JsonProperty("replaySteps", NullValueHandling = NullValueHandling.Ignore)] internal ReplayStep[]? Steps;
            [JsonProperty("commit", NullValueHandling = NullValueHandling.Ignore)] internal CommitObservation? Commit;
            [JsonProperty("presentation", NullValueHandling = NullValueHandling.Ignore)] internal PresentationObservation? Presentation;
        }

        /// <summary>
        /// 单一表现对象的离线观测。实例句柄属于表现注册表，不是逻辑实体 ID；空位置表示本 Tick 没有观测。
        /// </summary>
        internal sealed class PresentationObservation
        {
            [JsonProperty(Required = Required.Always)] internal string Phase = string.Empty;
            [JsonProperty(Required = Required.Always)] internal string CueCommand = string.Empty;
            [JsonProperty(Required = Required.Always)] internal string RejectReason = string.Empty;
            [JsonProperty(Required = Required.Always)] internal bool Promoted;
            [JsonProperty(Required = Required.Always)] internal bool DuplicateIgnored;
            [JsonProperty(Required = Required.AllowNull)] internal PresentationPoint? Authority;
            [JsonProperty(Required = Required.AllowNull)] internal PresentationPoint? View;
            [JsonProperty(Required = Required.AllowNull)] internal int? Handle;
            [JsonProperty(Required = Required.Always)] internal int Active;
            [JsonProperty(Required = Required.Always)] internal long Created;
            [JsonProperty(Required = Required.Always)] internal long Destroyed;
        }

        /// <summary>
        /// 世界坐标的离线绘图采样，单位为千分之一世界单位。
        /// </summary>
        internal sealed class PresentationPoint
        {
            [JsonProperty(Required = Required.Always)] internal int X;
            [JsonProperty(Required = Required.Always)] internal int Y;
            [JsonProperty(Required = Required.Always)] internal int Z;
        }

        /// <summary>
        /// 提交夹具的独立观测：准入修订、当前物化值和本次尝试阶段，不以快照流字段代替。
        /// </summary>
        internal sealed class CommitObservation
        {
            [JsonProperty(Required = Required.AllowNull)] internal long? AdmittedRevision;
            [JsonProperty(Required = Required.AllowNull)] internal int? AdmittedTick;
            [JsonProperty(Required = Required.AllowNull)] internal long? MaterializedRevision;
            [JsonProperty(Required = Required.AllowNull)] internal int? MaterializedPosition;
            [JsonProperty(Required = Required.Always)] internal string[] Stages = Array.Empty<string>();
        }

        /// <summary>
        /// 实际步进回调入口与出口的候选位置，不代表独立对外发布的预测结果。
        /// </summary>
        internal sealed class ReplayStep
        {
            [JsonProperty(Required = Required.Always)] internal int Tick;
            [JsonProperty(Required = Required.Always)] internal int Before;
            [JsonProperty(Required = Required.Always)] internal int After;
            [JsonProperty("afterPose", NullValueHandling = NullValueHandling.Ignore)] internal ActorPose? AfterPose = null;
        }

        /// <summary>
        /// 同一次交付内依次对照旧预测、显示重演起点和实际步进，最后显示交付后的检查点。
        /// </summary>
        internal enum FramePhase { Compare, Anchor, ReplayTick, Checkpoint }

        /// <summary>
        /// 时间线游标指向的实际观测；不插值、不为旧版文件合成重演位置。
        /// </summary>
        internal readonly struct Frame
        {
            internal readonly Checkpoint Point;
            internal readonly int DeliveryIndex;
            internal readonly FramePhase Phase;
            internal readonly ReplayStep? Step;
            internal readonly int? PreviousPrediction;
            internal readonly ActorPose? PredictionPose;

            internal Frame(Checkpoint point, int deliveryIndex, FramePhase phase, ReplayStep? step = null,
                int? previousPrediction = null, ActorPose? previousPose = null)
            {
                Point = point;
                DeliveryIndex = deliveryIndex;
                Phase = phase;
                Step = step;
                PreviousPrediction = previousPrediction;
                PredictionPose = phase switch
                {
                    FramePhase.Compare => previousPose,
                    FramePhase.Anchor => point.AuthorityPose,
                    FramePhase.ReplayTick => step?.AfterPose,
                    _ => point.PredictionPose,
                };
            }

            internal int? Position => Phase == FramePhase.Anchor ? Step?.Before : Step?.After;

            /// <summary>
            /// 回退起点直接复用权威姿态；该帧只绘制原色候选，避免完全重合的模型互相遮挡。
            /// </summary>
            internal bool SharesAnchorPose => Phase == FramePhase.Anchor && PredictionPose != null;

            internal ActorPose? AuthorityProjectionPose => SharesAnchorPose ? null : Point.AuthorityPose;
        }

        /// <summary>
        /// 一次交付在回放中的起点和最终检查点；没有重演时二者指向同一帧。
        /// </summary>
        internal readonly struct DeliveryRange
        {
            internal readonly int FirstFrame;
            internal readonly int CheckpointFrame;

            internal DeliveryRange(int firstFrame, int checkpointFrame)
            {
                FirstFrame = firstFrame;
                CheckpointFrame = checkpointFrame;
            }
        }

        /// <summary>
        /// 编辑器游标的导航索引。切换同用例产物时按命令、交付 Tick 和出现次数匹配，
        /// 不依赖两份产物拥有同样数量的重演步骤。
        /// </summary>
        internal sealed class Timeline
        {
            private readonly string _caseId;
            internal IReadOnlyList<Frame> Frames { get; }
            internal IReadOnlyList<DeliveryRange> Deliveries { get; }

            internal Timeline(string caseId, IReadOnlyList<Frame> frames, IReadOnlyList<DeliveryRange> deliveries)
            {
                _caseId = caseId;
                Frames = frames;
                Deliveries = deliveries;
            }

            internal int? FindMatchingCheckpoint(Timeline previous, int previousFrame)
            {
                if (!string.Equals(_caseId, previous._caseId, StringComparison.Ordinal) ||
                    previousFrame < 0 || previousFrame >= previous.Frames.Count)
                    return null;

                var source = previous.Frames[previousFrame];
                var ordinal = 0;
                for (var index = 0; index < source.DeliveryIndex; index++)
                    if (SameDelivery(source.Point, previous.Frames[previous.Deliveries[index].CheckpointFrame].Point))
                        ordinal++;

                foreach (var delivery in Deliveries)
                    if (SameDelivery(source.Point, Frames[delivery.CheckpointFrame].Point) && ordinal-- == 0)
                        return delivery.CheckpointFrame;

                return null;
            }

            private static bool SameDelivery(Checkpoint left, Checkpoint right) =>
                left.DeliveryTick == right.DeliveryTick && string.Equals(left.Command, right.Command, StringComparison.Ordinal);
        }
    }
}
