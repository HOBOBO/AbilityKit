using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 命令在派发点的执行类别；未知结果不能用于结算权威帧。
    /// </summary>
    public enum MobaFrameInputDisposition : byte
    {
        Unknown,
        Executed,
        Rejected,
        Duplicate
    }

    /// <summary>
    /// 帧内命令结果。仅实际执行项保留可重演的输入。
    /// </summary>
    public readonly struct MobaFrameInputOutcome
    {
        private readonly PlayerInputCommand _input;

        internal MobaFrameInputOutcome(int ordinal, MobaFrameInputDisposition disposition, PlayerInputCommand input)
        {
            Ordinal = ordinal;
            Disposition = disposition;
            _input = Copy(in input);
        }

        /// <summary>
        /// 命令在原提交列表中的位置。
        /// </summary>
        public int Ordinal { get; }

        /// <summary>
        /// 实际执行类别，不等同于输入提交回执。
        /// </summary>
        public MobaFrameInputDisposition Disposition { get; }

        /// <summary>
        /// 返回独立的执行负载；未执行项返回默认值。
        /// </summary>
        public PlayerInputCommand CopyExecutedInput() => Copy(in _input);

        private static PlayerInputCommand Copy(in PlayerInputCommand input) =>
            new PlayerInputCommand(input.Frame, input.Player, input.OpCode,
                input.Payload == null ? null : (byte[])input.Payload.Clone());
    }

    /// <summary>
    /// 权威所有者产生的决定或中断。序号等于输入数时属于世界推进阶段。
    /// </summary>
    public readonly struct MobaFrameAuthorityEntry
    {
        /// <summary>
        /// 将权威决定绑定到原始派发位置，供权威捕获和已解码的客户端记录共用。
        /// </summary>
        public MobaFrameAuthorityEntry(int ordinal, MobaActionAckEntry entry)
        {
            Ordinal = ordinal;
            Entry = entry;
        }

        /// <summary>
        /// 产生回执时的派发位置，或世界推进阶段的位置。
        /// </summary>
        public int Ordinal { get; }

        /// <summary>
        /// 复用权威所有者产生的原始决定，不重新分配序号。
        /// </summary>
        public MobaActionAckEntry Entry { get; }
    }

    /// <summary>
    /// 结算帧的原始提交数和权威决定时间线；不包含用于重放的玩法事件。
    /// </summary>
    public sealed class MobaSettledFrameMetadata : IWorldSettledFrameMetadata
    {
        /// <summary>
        /// 保存原提交位置及生成顺序，不依赖调用方继续持有同一列表。
        /// </summary>
        public MobaSettledFrameMetadata(int commandCount, IReadOnlyList<MobaFrameAuthorityEntry> entries)
        {
            if (commandCount < 0) throw new ArgumentOutOfRangeException(nameof(commandCount));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            CommandCount = commandCount;
            var copy = new MobaFrameAuthorityEntry[entries.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = entries[index];
            Entries = Array.AsReadOnly(copy);
        }

        /// <summary>
        /// 原始命令数，也是 Tick 阶段条目的序号。
        /// </summary>
        public int CommandCount { get; }

        /// <summary>
        /// 权威所有者观察到的决定和中断，保留重复决定的原引用。
        /// </summary>
        public IReadOnlyList<MobaFrameAuthorityEntry> Entries { get; }
    }

    /// <summary>
    /// 将单次派发和 Tick 产生的证据绑定到同一帧，不读取或清空待发 Ack 队列。
    /// </summary>
    public sealed class MobaAuthorityFrameCapture : IDisposable
    {
        private readonly MobaActionAckSnapshotService _acks;
        private readonly Func<IReadOnlyList<PlayerInputCommand>, Action<int>, Action<int, MobaInputCommandResult>, LogicWorldInputSubmitResult> _submit;
        private readonly List<MobaFrameInputOutcome> _outcomes = new List<MobaFrameInputOutcome>();
        private readonly List<MobaFrameAuthorityEntry> _entries = new List<MobaFrameAuthorityEntry>();
        private int _ordinal = -1;
        private bool _submitted;
        private bool _submissionComplete;
        private bool _disposed;

        internal MobaAuthorityFrameCapture(FrameIndex frame, MobaActionAckSnapshotService acks,
            Func<IReadOnlyList<PlayerInputCommand>, Action<int>, Action<int, MobaInputCommandResult>, LogicWorldInputSubmitResult> submit)
        {
            if (acks.AuthorityEntryCapture != null)
                throw new InvalidOperationException("An authority frame capture is already active.");
            Frame = frame;
            _acks = acks;
            _submit = submit;
            _acks.AuthorityEntryCapture = OnAuthorityEntry;
        }

        /// <summary>
        /// 捕获开始时绑定的世界帧。
        /// </summary>
        public FrameIndex Frame { get; }

        /// <summary>
        /// 提交一批权威输入。遇到未知结果时调用方必须停止结算，不可重试本批。
        /// </summary>
        public bool Submit(IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (_disposed || _submitted) throw new InvalidOperationException("Authority frame inputs may only be submitted once.");
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            _submitted = true;
            if (inputs.Count == 0)
            {
                _ordinal = 0;
                return _submissionComplete = true;
            }

            var result = _submit(inputs, ordinal => _ordinal = ordinal, (ordinal, command) =>
            {
                var disposition = Classify(command);
                if (disposition == MobaFrameInputDisposition.Unknown)
                    throw new InvalidOperationException($"Unproven input outcome at frame {Frame.Value}, ordinal {ordinal}: {command}.");
                var executed = disposition == MobaFrameInputDisposition.Executed ? inputs[ordinal] : default;
                _outcomes.Add(new MobaFrameInputOutcome(ordinal, disposition, executed));
            });
            _ordinal = inputs.Count;
            return _submissionComplete = result.Succeeded && _outcomes.Count == inputs.Count;
        }

        /// <summary>
        /// 只交付通过世界帧完成检查的证据。释放捕获不代表帧成功。
        /// </summary>
        public bool TryComplete(bool worldFrameCompleted, out IReadOnlyList<MobaFrameInputOutcome> outcomes,
            out IReadOnlyList<MobaFrameAuthorityEntry> entries)
        {
            outcomes = null;
            entries = null;
            if (_disposed) return false;
            Dispose();
            if (!worldFrameCompleted || !_submissionComplete) return false;
            outcomes = _outcomes.AsReadOnly();
            entries = _entries.AsReadOnly();
            return true;
        }

        /// <summary>
        /// 解除权威所有者对当前捕获的绑定，不更改既有玩法状态或回执队列。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _acks.AuthorityEntryCapture = null;
        }

        private void OnAuthorityEntry(MobaActionAckEntry entry) =>
            _entries.Add(new MobaFrameAuthorityEntry(_ordinal, entry));

        private static MobaFrameInputDisposition Classify(in MobaInputCommandResult result)
        {
            if (result.IsDuplicate) return MobaFrameInputDisposition.Duplicate;
            if (result.Succeeded) return MobaFrameInputDisposition.Executed;
            switch (result.FailureCode)
            {
                case MobaInputCommandFailureCode.AuthorityRejected:
                case MobaInputCommandFailureCode.FramePolicyRejected:
                case MobaInputCommandFailureCode.PredictionIdentityRejected:
                case MobaInputCommandFailureCode.InputSequenceRejected:
                case MobaInputCommandFailureCode.InterruptEpochRejected:
                case MobaInputCommandFailureCode.PayloadInvalid:
                case MobaInputCommandFailureCode.PayloadMissing:
                    return MobaFrameInputDisposition.Rejected;
                default:
                    return MobaFrameInputDisposition.Unknown;
            }
        }
    }
}
