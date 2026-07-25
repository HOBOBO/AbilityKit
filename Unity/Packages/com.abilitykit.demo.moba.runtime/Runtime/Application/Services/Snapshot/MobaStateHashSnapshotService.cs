using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(70)]
    [WorldService(typeof(MobaStateHashSnapshotService))]
    public sealed class MobaStateHashSnapshotService : IService, IMobaSnapshotEmitter
    {
        private const int DefaultIntervalFrames = 10;
        private static readonly Comparison<StateHashEntry> CompareEntriesByActorId =
            (a, b) => a.ActorId.CompareTo(b.ActorId);

        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaActorRegistry _registry;
        private readonly IMobaStateRecoveryProvider _randomRecovery;
        private readonly MobaBuffStateRecoveryProvider _buffRecovery;
        private readonly MobaSnapshotBuffer<StateHashEntry> _entries = new MobaSnapshotBuffer<StateHashEntry>(16, 256);

        private FrameIndex _lastFrame;

        public int IntervalFrames { get; set; } = DefaultIntervalFrames;

        public MobaStateHashSnapshotService(
            MobaLogicWorldRunGateService phase,
            MobaActorRegistry registry,
            IWorldRandom random,
            MobaBuffStateRecoveryProvider buffRecovery)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _randomRecovery = random as IMobaStateRecoveryProvider
                ?? throw new ArgumentException("World random must implement IMobaStateRecoveryProvider.", nameof(random));
            _buffRecovery = buffRecovery ?? throw new ArgumentNullException(nameof(buffRecovery));
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }

            var interval = IntervalFrames;
            if (interval <= 0) interval = DefaultIntervalFrames;

            if ((frame.Value % interval) != 0)
            {
                snapshot = default;
                return false;
            }

            _lastFrame = frame;

            var hash = ComputeStateHash(frame);
            var payload = MobaStateHashSnapshotCodec.Serialize(frame.Value, hash);
            snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.StateHash, payload);
            return true;
        }

        private uint ComputeStateHash(FrameIndex frame)
        {
            var hash = new MobaStateHashBuilder(2166136261u);
            hash.AddBool(_phase.InGame);
            AddActorTransformHash(hash);
            // Random 和 Buff 哈希暂时不加入——客户端 RemoteDrivenStateHashFactory 无法访问
            // MobaBuffStateRecoveryProvider，两端不对齐会导致 reconcile 永远误判 mismatch。
            // 它们的回滚 provider 仍在做恢复（RollbackWorldRandom / BuffTimer），
            // 如果不一致最终会通过 Transform/HP 漂移间接发现。
            // _randomRecovery.AddStateHash(frame, hash);
            // _buffRecovery.AddStateHash(frame, hash);
            return hash.Value;
        }

        private void AddActorTransformHash(MobaStateHashBuilder hash)
        {
            _entries.Clear();
            foreach (var kv in _registry.Entries)
            {
                var actorId = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;

                float hp = 0f;
                if (e.hasAttributeGroup && e.attributeGroup.Group != null)
                {
                    hp = e.attributeGroup.Group.GetValue(MobaAttributeIds.HP);
                }
                _entries.Add(new StateHashEntry(actorId, e.transform.Value, hp));
            }

            _entries.Sort(CompareEntriesByActorId);

            var entries = _entries.Items;
            hash.AddInt(MobaActorTransformRollbackProvider.DefaultKey);
            hash.AddInt(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var it = entries[i];
                hash.AddInt(it.ActorId);
                hash.AddFloat(it.X);
                hash.AddFloat(it.Y);
                hash.AddFloat(it.Z);
                hash.AddFloat(it.Rx);
                hash.AddFloat(it.Ry);
                hash.AddFloat(it.Rz);
                hash.AddFloat(it.Rw);
                hash.AddFloat(it.Sx);
                hash.AddFloat(it.Sy);
                hash.AddFloat(it.Sz);
                hash.AddFloat(it.Hp);
            }

            _entries.ClearAndTrim();
        }

        public void Dispose()
        {
            _entries.Clear();
            _lastFrame = new FrameIndex(-999999);
        }

        private readonly struct StateHashEntry
        {
            public readonly int ActorId;
            public readonly float X;
            public readonly float Y;
            public readonly float Z;
            public readonly float Rx;
            public readonly float Ry;
            public readonly float Rz;
            public readonly float Rw;
            public readonly float Sx;
            public readonly float Sy;
            public readonly float Sz;
            public readonly float Hp;

            public StateHashEntry(int actorId, in AbilityKit.Core.Mathematics.Transform3 transform, float hp)
            {
                ActorId = actorId;
                X = transform.Position.X;
                Y = transform.Position.Y;
                Z = transform.Position.Z;
                Rx = transform.Rotation.X;
                Ry = transform.Rotation.Y;
                Rz = transform.Rotation.Z;
                Rw = transform.Rotation.W;
                Sx = transform.Scale.X;
                Sy = transform.Scale.Y;
                Sz = transform.Scale.Z;
                Hp = hp;
            }
        }
    }
}
