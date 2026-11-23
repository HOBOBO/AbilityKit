using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Network.Abstractions;
using AbilityKit.Protocol.Moba.SettledFrames;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 绑定一个已协商的 MOBA 结算流。实时和补帧须经同一校验；
    /// 接收者不合成空帧，不推进模拟，也不允许更换战局或代次。
    /// </summary>
    public sealed class MobaSettledFrameReceiver : IConsumableRemoteFrameSource<WorldSettledInputFrame>
    {
        private readonly WireSettledIdentity _identity;
        private readonly int _capacity;
        private readonly Dictionary<int, Record> _pending = new Dictionary<int, Record>();
        private readonly Dictionary<int, byte[]> _recentlyConsumed = new Dictionary<int, byte[]>();
        private bool _faulted;
        private bool _disposed;
        private int _delayFrames;

        private sealed class Record
        {
            public WorldSettledInputFrame Frame;
            public byte[] Digest;
        }

        /// <summary>
        /// 为新客户端世界绑定订阅应答；必须收到显式帧零证明后才可消费。
        /// </summary>
        public MobaSettledFrameReceiver(WireSettledSubscribeResponse subscription, int capacity = 4096)
        {
            _identity = subscription.Identity;
            if (_identity.Version != SettledFrameProtocol.Version || _identity.RoomId == 0 || _identity.WorldId == 0 ||
                string.IsNullOrWhiteSpace(_identity.BattleId) || !Guid.TryParseExact(_identity.RuntimeGeneration, "N", out var generation) ||
                generation == Guid.Empty || subscription.TickRate <= 0 || subscription.LastSettledFrame < 0 || capacity <= 0)
                throw new ArgumentException("Invalid MOBA settled subscription.", nameof(subscription));
            _capacity = capacity;
        }

        /// <summary>
        /// 获取或设置实时延迟。冷补帧期间由调用方设为零。
        /// </summary>
        public int DelayFrames { get => _delayFrames; set => _delayFrames = Math.Max(0, value); }

        public bool HasCheckpoint { get; private set; }
        public int LastConsumedFrame { get; private set; }
        public int MaxReceivedFrame { get; private set; }
        public int TargetFrame => Math.Max(0, MaxReceivedFrame - _delayFrames);

        /// <summary>
        /// 校验实时帧并并入缓冲；若发现内容冲突，此接收者永久拒绝继续消费。
        /// </summary>
        public void Receive(in WireSettledFrame frame) => Merge(new[] { frame });

        /// <summary>
        /// 先校验整段身份和连续性，再与实时帧合并；零部分写入。
        /// </summary>
        public void ReceiveCatchUp(in WireSettledCatchUpResponse response, int fromFrameExclusive, int toFrameInclusive)
        {
            var frames = response.Frames;
            if (frames == null || frames.Length == 0 || fromFrameExclusive < -1 || toFrameInclusive <= fromFrameExclusive ||
                (long)frames.Length > (long)toFrameInclusive - fromFrameExclusive)
                throw Fault("Invalid MOBA catch-up range.");
            for (var index = 0; index < frames.Length; index++)
                if (frames[index].Frame != fromFrameExclusive + index + 1)
                    throw Fault("MOBA catch-up is not contiguous.");
            Merge(frames);
        }

        private void Merge(IReadOnlyList<WireSettledFrame> frames)
        {
            if (_disposed || _faulted) throw new InvalidOperationException("MOBA settled receiver is not usable.");
            var additions = new Dictionary<int, Record>();
            using var hasher = SHA256.Create();
            foreach (var wire in frames)
            {
                if (!Matches(wire.Identity) || wire.Frame < 0 || wire.InitialCheckpoint != (wire.Frame == 0) ||
                    wire.Commands == null || wire.Decisions == null ||
                    (wire.InitialCheckpoint && (wire.Commands.Length != 0 || wire.Decisions.Length != 0)))
                    throw Fault("Invalid settled frame identity or checkpoint.");

                var digest = hasher.ComputeHash(MemoryPackSerializer.Serialize(wire));
                if (_recentlyConsumed.TryGetValue(wire.Frame, out var consumed))
                {
                    if (!digest.AsSpan().SequenceEqual(consumed)) throw Fault("Conflicting consumed settled frame.");
                    continue;
                }
                if (_pending.TryGetValue(wire.Frame, out var existing) || additions.TryGetValue(wire.Frame, out existing))
                {
                    if (!digest.AsSpan().SequenceEqual(existing.Digest)) throw Fault("Conflicting pending settled frame.");
                    continue;
                }
                if (wire.Frame != 0 && wire.Frame <= LastConsumedFrame) throw Fault("Settled overlap is outside retained history.");
                additions.Add(wire.Frame, new Record { Frame = Decode(wire), Digest = digest });
            }
            if (_pending.Count + additions.Count - (additions.ContainsKey(0) ? 1 : 0) > _capacity)
                throw Fault("Settled input history capacity exceeded.");

            foreach (var addition in additions)
            {
                if (addition.Key == 0)
                {
                    HasCheckpoint = true;
                    _recentlyConsumed.Add(0, addition.Value.Digest);
                }
                else _pending.Add(addition.Key, addition.Value);
                MaxReceivedFrame = Math.Max(MaxReceivedFrame, addition.Key);
            }
        }

        private bool Matches(in WireSettledIdentity identity) => identity.Version == _identity.Version &&
            identity.RoomId == _identity.RoomId && identity.WorldId == _identity.WorldId &&
            identity.BattleId == _identity.BattleId && identity.RuntimeGeneration == _identity.RuntimeGeneration;

        private WorldSettledInputFrame Decode(in WireSettledFrame wire)
        {
            var frame = new FrameIndex(wire.Frame);
            var executed = new List<PlayerInputCommand>();
            for (var index = 0; index < wire.Commands.Length; index++)
            {
                var command = wire.Commands[index];
                if (command.Ordinal != index || command.PlayerId == 0 || command.OpCode <= 0 || command.ExecutedPayload == null ||
                    command.Disposition < 1 || command.Disposition > 3 || (command.Disposition != 1 && command.ExecutedPayload.Length != 0))
                    throw Fault("Invalid settled command.");
                if (command.Disposition == 1)
                    executed.Add(new PlayerInputCommand(frame, new PlayerId(command.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        command.OpCode, (byte[])command.ExecutedPayload.Clone()));
            }

            var entries = new MobaFrameAuthorityEntry[wire.Decisions.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var decision = wire.Decisions[index];
                if (decision.Ordinal < 0 || decision.Ordinal > wire.Commands.Length ||
                    (decision.PredictionKey > 0 && (decision.Ordinal == wire.Commands.Length ||
                     (wire.Commands[decision.Ordinal].Disposition != 3 && (wire.Commands[decision.Ordinal].Disposition == 1) != decision.Accepted))))
                    throw Fault("Settled decision is not bound to its command.");
                entries[index] = new MobaFrameAuthorityEntry(decision.Ordinal, new MobaActionAckEntry
                {
                    ActorId = decision.ActorId, EntityVersion = decision.EntityVersion, PredictionKey = decision.PredictionKey,
                    SkillId = decision.SkillId, Accepted = decision.Accepted, AuthoritativeFrame = decision.AuthoritativeFrame,
                    Reason = decision.Reason, InterruptEpoch = decision.InterruptEpoch, AuthoritySequence = decision.AuthoritySequence
                });
            }
            return new WorldSettledInputFrame(frame, executed.AsReadOnly(), new MobaSettledFrameMetadata(wire.Commands.Length, entries));
        }

        public bool TryGet(int frame, out WorldSettledInputFrame frameData)
        {
            frameData = null;
            if (_disposed || _faulted || !HasCheckpoint || !_pending.TryGetValue(frame, out var record)) return false;
            frameData = record.Frame;
            return true;
        }

        public bool TryConsume(int frame, out WorldSettledInputFrame frameData)
        {
            frameData = null;
            if (frame != LastConsumedFrame + 1 || !TryGet(frame, out frameData)) return false;
            _recentlyConsumed.Add(frame, _pending[frame].Digest);
            _pending.Remove(frame);
            LastConsumedFrame = frame;
            if (frame > _capacity) _recentlyConsumed.Remove(frame - _capacity);
            return true;
        }

        public void TrimBefore(int minFrameInclusive)
        {
            /* 未确认帧不得被裁剪。已消费指纹由有限窗口自动淘汰。 */
        }

        public void Dispose()
        {
            _disposed = true;
            _pending.Clear();
            _recentlyConsumed.Clear();
        }

        private InvalidOperationException Fault(string reason)
        {
            _faulted = true;
            return new InvalidOperationException(reason);
        }
    }
}
