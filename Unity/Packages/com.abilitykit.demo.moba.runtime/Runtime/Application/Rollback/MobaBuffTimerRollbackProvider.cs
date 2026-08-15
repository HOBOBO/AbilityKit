using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Continuous;
using AbilityKit.Core.Pooling;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Restores mutable state for Buff instances that still exist at rollback time.
    /// Instance membership and Continuous bindings are lifecycle-owned, so an unsafe
    /// shape change fails fast instead of silently producing a partial restoration.
    /// </summary>
    public sealed class MobaBuffTimerRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10003;
        // v3 (2026-08-15): 剩余/间隔剩余以 Q32.32 raw long 存储（无限期时长用 long.MinValue 哨兵）。
        private const int CurrentPayloadVersion = 3;
        private const long InfiniteRemainingRaw = long.MinValue;

        private static readonly ObjectPool<List<MobaBuffTimerRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaBuffTimerRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        public MobaBuffTimerRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;
        public string Name => "BuffRuntime";

        public byte[] Export(FrameIndex frame)
        {
            return ExportState(frame);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            ImportState(frame, payload);
        }

        public byte[] ExportState(FrameIndex frame)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var actorId = kv.Key;
                    var actor = kv.Value;
                    if (actor == null || !actor.hasBuffs) continue;

                    var active = actor.buffs.Active;
                    if (active == null) continue;

                    for (var i = 0; i < active.Count; i++)
                    {
                        var buff = active[i];
                        if (buff == null) continue;
                        if (buff.BuffId <= 0)
                        {
                            throw new InvalidOperationException(
                                $"Cannot capture rollback state for an invalid BuffRuntime. actor={actorId} index={i} buffId={buff.BuffId}.");
                        }

                        var continuous = buff.Continuous;
                        long remainingRaw;
                        long intervalRemainingRaw;
                        if (continuous != null)
                        {
                            var continuousRemaining = continuous.RemainingRaw;
                            remainingRaw = continuousRemaining.HasValue ? continuousRemaining.Value : InfiniteRemainingRaw;
                            intervalRemainingRaw = continuous.IntervalRemainingRaw;
                        }
                        else
                        {
                            remainingRaw = Core.Mathematics.DeterministicMathBridge.ToFixed(buff.Remaining).RawValue;
                            intervalRemainingRaw = Core.Mathematics.DeterministicMathBridge.ToFixed(buff.IntervalRemainingSeconds).RawValue;
                        }

                        var maxStack = continuous?.Config is IStackConfig stackConfig
                            ? stackConfig.MaxStack
                            : Math.Max(1, buff.StackCount);

                        entries.Add(new MobaBuffTimerRollbackEntry(
                            actorId,
                            buff.BuffId,
                            remainingRaw,
                            intervalRemainingRaw,
                            buff.StackCount,
                            buff.SourceId,
                            buff.SourceContextId,
                            buff.RuntimeContextId,
                            buff.RuntimeContextVersion,
                            continuous != null,
                            continuous != null ? (int)continuous.State : 0,
                            maxStack));
                    }
                }

                entries.Sort(CompareEntries);
                ValidateSnapshotIdentities(entries);
                var snapshotEntries = entries.Count == 0
                    ? Array.Empty<MobaBuffTimerRollbackEntry>()
                    : entries.ToArray();
                return MemoryPackSerializer.Serialize(
                    new MobaBuffTimerRollbackPayload(CurrentPayloadVersion, snapshotEntries));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                throw new InvalidOperationException("Buff rollback payload is missing.");
            }

            var snapshot = MemoryPackSerializer.Deserialize<MobaBuffTimerRollbackPayload>(payload);
            if (snapshot.Version != CurrentPayloadVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported Buff rollback payload version: {snapshot.Version}. Expected {CurrentPayloadVersion}.");
            }

            var entries = snapshot.Entries ?? Array.Empty<MobaBuffTimerRollbackEntry>();
            ValidateSnapshotIdentities(entries);

            var current = BuildCurrentIndex();
            if (current.Count != entries.Length)
            {
                throw new InvalidOperationException(
                    $"Buff instance membership changed since capture. snapshot={entries.Length} current={current.Count}. " +
                    "Rollback cannot rebuild lifecycle-owned Buff instances safely.");
            }

            var matches = new BuffRuntime[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var identity = BuffIdentity.FromEntry(entry);
                if (!current.TryGetValue(identity, out var runtime))
                {
                    throw new InvalidOperationException(
                        $"Buff instance is missing during rollback. {identity}. " +
                        "Rollback cannot rebuild lifecycle-owned Buff instances safely.");
                }

                ValidateContinuousBinding(entry, runtime, identity);
                matches[i] = runtime;
            }

            // All shape and binding checks complete before the first mutation.
            for (var i = 0; i < entries.Length; i++)
            {
                Apply(entries[i], matches[i]);
            }
        }

        private Dictionary<BuffIdentity, BuffRuntime> BuildCurrentIndex()
        {
            var result = new Dictionary<BuffIdentity, BuffRuntime>();
            foreach (var kv in _registry.Entries)
            {
                var actorId = kv.Key;
                var actor = kv.Value;
                if (actor == null || !actor.hasBuffs || actor.buffs.Active == null) continue;

                var active = actor.buffs.Active;
                for (var i = 0; i < active.Count; i++)
                {
                    var runtime = active[i];
                    if (runtime == null) continue;

                    var identity = BuffIdentity.FromRuntime(actorId, runtime);
                    if (!result.TryAdd(identity, runtime))
                    {
                        throw new InvalidOperationException(
                            $"Current Buff instances do not have stable unique identities. {identity}.");
                    }
                }
            }

            return result;
        }

        private static void ValidateSnapshotIdentities(IReadOnlyList<MobaBuffTimerRollbackEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            var identities = new HashSet<BuffIdentity>();
            for (var i = 0; i < entries.Count; i++)
            {
                var identity = BuffIdentity.FromEntry(entries[i]);
                if (!identities.Add(identity))
                {
                    throw new InvalidOperationException(
                        $"Captured Buff instances do not have stable unique identities. {identity}.");
                }
            }
        }

        private static void ValidateContinuousBinding(
            in MobaBuffTimerRollbackEntry entry,
            BuffRuntime runtime,
            in BuffIdentity identity)
        {
            var continuous = runtime.Continuous;
            if (entry.HasContinuous != (continuous != null))
            {
                throw new InvalidOperationException(
                    $"Buff Continuous binding changed since capture. {identity} snapshot={entry.HasContinuous} current={continuous != null}.");
            }

            if (continuous == null) return;
            if (!ReferenceEquals(continuous.Runtime, runtime))
            {
                throw new InvalidOperationException($"Buff Continuous runtime points at a different Buff instance. {identity}.");
            }

            if (continuous.BuffId != entry.BuffId ||
                continuous.TargetActorId != entry.ActorId ||
                continuous.SourceContextId != entry.SourceContextId)
            {
                throw new InvalidOperationException($"Buff Continuous identity changed since capture. {identity}.");
            }

            if ((int)continuous.State != entry.ContinuousState)
            {
                throw new InvalidOperationException(
                    $"Buff Continuous lifecycle state changed since capture. {identity} " +
                    $"snapshot={entry.ContinuousState} current={(int)continuous.State}.");
            }
        }

        private static void Apply(in MobaBuffTimerRollbackEntry entry, BuffRuntime runtime)
        {
            runtime.SourceId = entry.SourceActorId;
            runtime.StackCount = entry.StackCount;
            runtime.RuntimeContextVersion = entry.RuntimeContextVersion;

            var continuous = runtime.Continuous;
            if (continuous == null)
            {
                runtime.Remaining = Deterministic.Fixed64.FromRaw(entry.RemainingRaw).ToSingle();
                runtime.IntervalRemainingSeconds = Deterministic.Fixed64.FromRaw(entry.IntervalRemainingRaw).ToSingle();
                return;
            }

            var remainingRaw = entry.RemainingRaw == InfiniteRemainingRaw ? (long?)null : entry.RemainingRaw;
            continuous.RefreshRaw(
                entry.SourceActorId,
                remainingRaw,
                entry.StackCount,
                entry.ContinuousMaxStack,
                runtime.TagRequirements);
            continuous.IntervalRemainingRaw = entry.IntervalRemainingRaw;
            continuous.SyncManagedState();
        }

        private static int CompareEntries(MobaBuffTimerRollbackEntry a, MobaBuffTimerRollbackEntry b)
        {
            var c = a.ActorId.CompareTo(b.ActorId);
            if (c != 0) return c;
            c = a.BuffId.CompareTo(b.BuffId);
            if (c != 0) return c;
            c = a.SourceContextId.CompareTo(b.SourceContextId);
            return c != 0 ? c : a.RuntimeContextId.CompareTo(b.RuntimeContextId);
        }

        private readonly struct BuffIdentity : IEquatable<BuffIdentity>
        {
            private BuffIdentity(int actorId, int buffId, long sourceContextId, long runtimeContextId)
            {
                ActorId = actorId;
                BuffId = buffId;
                SourceContextId = sourceContextId;
                RuntimeContextId = runtimeContextId;
            }

            private int ActorId { get; }
            private int BuffId { get; }
            private long SourceContextId { get; }
            private long RuntimeContextId { get; }

            public static BuffIdentity FromRuntime(int actorId, BuffRuntime runtime)
            {
                return new BuffIdentity(actorId, runtime.BuffId, runtime.SourceContextId, runtime.RuntimeContextId);
            }

            public static BuffIdentity FromEntry(in MobaBuffTimerRollbackEntry entry)
            {
                return new BuffIdentity(entry.ActorId, entry.BuffId, entry.SourceContextId, entry.RuntimeContextId);
            }

            public bool Equals(BuffIdentity other)
            {
                return ActorId == other.ActorId &&
                       BuffId == other.BuffId &&
                       SourceContextId == other.SourceContextId &&
                       RuntimeContextId == other.RuntimeContextId;
            }

            public override bool Equals(object obj)
            {
                return obj is BuffIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = ActorId;
                    hash = (hash * 397) ^ BuffId;
                    hash = (hash * 397) ^ SourceContextId.GetHashCode();
                    return (hash * 397) ^ RuntimeContextId.GetHashCode();
                }
            }

            public override string ToString()
            {
                return $"actor={ActorId} buffId={BuffId} sourceContextId={SourceContextId} runtimeContextId={RuntimeContextId}";
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBuffTimerRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaBuffTimerRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaBuffTimerRollbackPayload(int version, MobaBuffTimerRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBuffTimerRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly int BuffId;
        [MemoryPackOrder(2)] public readonly long RemainingRaw;
        [MemoryPackOrder(3)] public readonly long IntervalRemainingRaw;
        [MemoryPackOrder(4)] public readonly int StackCount;
        [MemoryPackOrder(5)] public readonly int SourceActorId;
        [MemoryPackOrder(6)] public readonly long SourceContextId;
        [MemoryPackOrder(7)] public readonly long RuntimeContextId;
        [MemoryPackOrder(8)] public readonly long RuntimeContextVersion;
        [MemoryPackOrder(9)] public readonly bool HasContinuous;
        [MemoryPackOrder(10)] public readonly int ContinuousState;
        [MemoryPackOrder(11)] public readonly int ContinuousMaxStack;

        public MobaBuffTimerRollbackEntry(
            int actorId,
            int buffId,
            long remainingRaw,
            long intervalRemainingRaw,
            int stackCount,
            int sourceActorId,
            long sourceContextId,
            long runtimeContextId,
            long runtimeContextVersion,
            bool hasContinuous,
            int continuousState,
            int continuousMaxStack)
        {
            ActorId = actorId;
            BuffId = buffId;
            RemainingRaw = remainingRaw;
            IntervalRemainingRaw = intervalRemainingRaw;
            StackCount = stackCount;
            SourceActorId = sourceActorId;
            SourceContextId = sourceContextId;
            RuntimeContextId = runtimeContextId;
            RuntimeContextVersion = runtimeContextVersion;
            HasContinuous = hasContinuous;
            ContinuousState = continuousState;
            ContinuousMaxStack = continuousMaxStack;
        }

        public MobaBuffTimerRollbackEntry(
            int actorId,
            int buffId,
            long remainingRaw,
            long intervalRemainingRaw,
            int stackCount)
            : this(actorId, buffId, remainingRaw, intervalRemainingRaw, stackCount, 0, 0L, 0L, 0L, false, 0, 1)
        {
        }
    }
}
