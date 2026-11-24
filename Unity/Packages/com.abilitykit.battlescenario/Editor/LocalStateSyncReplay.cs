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
        [JsonProperty(Required = Required.Always)] internal Checkpoint[] Checkpoints = Array.Empty<Checkpoint>();

        internal bool HasStepPositions => Version == CurrentVersion;

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
                var previousPrediction = prior?.Generation == point.Generation ? prior.Prediction : null;
                if (point.Steps is { Length: > 0 } steps)
                {
                    frames.Add(new Frame(point, deliveryIndex, FramePhase.Anchor, steps[0], previousPrediction));
                    foreach (var step in steps)
                        frames.Add(new Frame(point, deliveryIndex, FramePhase.ReplayTick, step, previousPrediction));
                }
                deliveries[deliveryIndex] = new DeliveryRange(firstFrame, frames.Count);
                frames.Add(new Frame(point, deliveryIndex, FramePhase.Checkpoint, previousPrediction: previousPrediction));
            }
            return new Timeline(CaseId, frames, deliveries);
        }

        private void Validate()
        {
            if (Format != FormatId || (Version != CheckpointOnlyVersion && Version != CurrentVersion))
                throw new InvalidDataException("不支持的本地状态同步回放格式或版本。");
            if (string.IsNullOrWhiteSpace(CaseId) || string.IsNullOrWhiteSpace(Variant) || TickRate <= 0 || Checkpoints.Length == 0)
                throw new InvalidDataException("本地状态同步回放缺少用例、变体、Tick 频率或检查点。");

            var previousTick = 0;
            foreach (var point in Checkpoints)
            {
                if (point == null || string.IsNullOrWhiteSpace(point.Command) || point.DeliveryTick < previousTick)
                    throw new InvalidDataException("本地状态同步回放存在空检查点、无名命令或倒序检查点。");
                ValidateSteps(point);
                previousTick = point.DeliveryTick;
            }
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
            [JsonProperty(Required = Required.Always)] internal long[] Pending = Array.Empty<long>();
            [JsonProperty(Required = Required.Always)] internal int[] ReplayTicks = Array.Empty<int>();
            [JsonProperty("replaySteps", NullValueHandling = NullValueHandling.Ignore)] internal ReplayStep[]? Steps;
            [JsonProperty("commit", NullValueHandling = NullValueHandling.Ignore)] internal CommitObservation? Commit;
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
        }

        /// <summary>
        /// 同一次交付内的显示阶段：先显示重演起点和实际步进，最后显示交付后的检查点。
        /// </summary>
        internal enum FramePhase { Anchor, ReplayTick, Checkpoint }

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

            internal Frame(Checkpoint point, int deliveryIndex, FramePhase phase, ReplayStep? step = null, int? previousPrediction = null)
            {
                Point = point;
                DeliveryIndex = deliveryIndex;
                Phase = phase;
                Step = step;
                PreviousPrediction = previousPrediction;
            }

            internal int? Position => Phase == FramePhase.Anchor ? Step?.Before : Step?.After;
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
