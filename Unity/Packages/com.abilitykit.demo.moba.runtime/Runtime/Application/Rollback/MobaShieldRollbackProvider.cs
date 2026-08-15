using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Restores the service-owned shield containers for the current actor set.
    /// Actor collection changes fail fast because this provider cannot safely
    /// create or destroy simulation entities.
    /// </summary>
    public sealed class MobaShieldRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10006;
        // v2 (2026-08-15): 护盾数值字段定点化（Q32.32），快照以 raw long 存储（TotalRemaining/TransferRatio/CurrentValue/MaxValue/InitialValue/AbsorbRatio）。
        private const int CurrentPayloadVersion = 2;

        private readonly MobaActorRegistry _actors;
        private readonly MobaShieldService _shields;

        public MobaShieldRollbackProvider(MobaActorRegistry actors, MobaShieldService shields)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _shields = shields ?? throw new ArgumentNullException(nameof(shields));
        }

        public int Key => DefaultKey;
        public string Name => "Shield";

        public byte[] Export(FrameIndex frame)
        {
            var entries = new List<MobaShieldRollbackEntry>();
            foreach (var kv in _actors.Entries)
            {
                var actorId = kv.Key;
                if (!_shields.TryGetContainer(actorId, out var container))
                {
                    entries.Add(MobaShieldRollbackEntry.Empty(actorId));
                    continue;
                }

                entries.Add(MobaShieldRollbackEntry.FromContainer(actorId, container));
            }

            entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
            ValidateEntries(entries);
            return MemoryPackSerializer.Serialize(new MobaShieldRollbackPayload(
                CurrentPayloadVersion,
                entries.Count == 0 ? Array.Empty<MobaShieldRollbackEntry>() : entries.ToArray()));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                throw new InvalidOperationException("Shield rollback payload is missing.");
            }

            var snapshot = MemoryPackSerializer.Deserialize<MobaShieldRollbackPayload>(payload);
            if (snapshot.Version != CurrentPayloadVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported Shield rollback payload version: {snapshot.Version}. Expected {CurrentPayloadVersion}.");
            }

            var entries = snapshot.Entries ?? Array.Empty<MobaShieldRollbackEntry>();
            ValidateEntries(entries);
            ValidateActorSet(entries);

            var restored = BuildContainers(entries);
            _shields.RestoreContainers(restored);
        }

        private static Fixed64 FromRaw(long raw)
        {
            return Fixed64.FromRaw(raw);
        }

        private void ValidateActorSet(IReadOnlyList<MobaShieldRollbackEntry> entries)
        {
            var currentActorIds = new List<int>();
            foreach (var kv in _actors.Entries)
            {
                currentActorIds.Add(kv.Key);
            }

            currentActorIds.Sort();
            if (currentActorIds.Count != entries.Count)
            {
                throw new InvalidOperationException(
                    $"Actor collection changed since Shield capture. snapshot={entries.Count} current={currentActorIds.Count}. " +
                    "Rollback does not own entity creation or destruction.");
            }

            for (var i = 0; i < currentActorIds.Count; i++)
            {
                if (currentActorIds[i] == entries[i].ActorId) continue;
                throw new InvalidOperationException(
                    $"Actor collection changed since Shield capture. snapshotActor={entries[i].ActorId} " +
                    $"currentActor={currentActorIds[i]}. Rollback does not own entity creation or destruction.");
            }
        }

        private static Dictionary<int, ShieldContainer> BuildContainers(
            IReadOnlyList<MobaShieldRollbackEntry> entries)
        {
            var restored = new Dictionary<int, ShieldContainer>();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                if (!entry.HasContainer) continue;

                var sourceLayers = entry.Layers ?? Array.Empty<MobaShieldLayerRollbackEntry>();
                var layers = new List<ShieldLayer>(sourceLayers.Length);
                for (var layerIndex = 0; layerIndex < sourceLayers.Length; layerIndex++)
                {
                    layers.Add(sourceLayers[layerIndex].ToLayer());
                }

                restored.Add(entry.ActorId, new ShieldContainer
                {
                    Layers = layers,
                    NextInstanceId = entry.NextInstanceId,
                    TotalRemaining = FromRaw(entry.TotalRemainingRaw),
                    Dirty = entry.Dirty,
                });
            }

            return restored;
        }

        private static void ValidateEntries(IReadOnlyList<MobaShieldRollbackEntry> entries)
        {
            var previousActorId = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0 || entry.ActorId <= previousActorId)
                {
                    throw new InvalidOperationException(
                        $"Shield rollback actor entries must be unique and ordered. actor={entry.ActorId} index={i}.");
                }

                previousActorId = entry.ActorId;
                if (!entry.HasContainer)
                {
                    if (entry.Layers != null && entry.Layers.Length > 0)
                    {
                        throw new InvalidOperationException(
                            $"Shield rollback entry without a container has layer state. actor={entry.ActorId}.");
                    }

                    continue;
                }

                var layers = entry.Layers ?? Array.Empty<MobaShieldLayerRollbackEntry>();
                var instanceIds = new HashSet<int>();
                for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    var layer = layers[layerIndex];
                    if (layer.InstanceId <= 0 || !instanceIds.Add(layer.InstanceId))
                    {
                        throw new InvalidOperationException(
                            $"Shield layer identities must be positive and unique. actor={entry.ActorId} " +
                            $"instanceId={layer.InstanceId} index={layerIndex}.");
                    }
                }
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaShieldRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaShieldRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaShieldRollbackPayload(int version, MobaShieldRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaShieldRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly bool HasContainer;
        [MemoryPackOrder(2)] public readonly int NextInstanceId;
        [MemoryPackOrder(3)] public readonly long TotalRemainingRaw;
        [MemoryPackOrder(4)] public readonly bool Dirty;
        [MemoryPackOrder(5)] public readonly MobaShieldLayerRollbackEntry[] Layers;

        [MemoryPackConstructor]
        public MobaShieldRollbackEntry(
            int actorId,
            bool hasContainer,
            int nextInstanceId,
            long totalRemainingRaw,
            bool dirty,
            MobaShieldLayerRollbackEntry[] layers)
        {
            ActorId = actorId;
            HasContainer = hasContainer;
            NextInstanceId = nextInstanceId;
            TotalRemainingRaw = totalRemainingRaw;
            Dirty = dirty;
            Layers = layers;
        }

        public static MobaShieldRollbackEntry Empty(int actorId)
        {
            return new MobaShieldRollbackEntry(
                actorId,
                false,
                0,
                0L,
                false,
                Array.Empty<MobaShieldLayerRollbackEntry>());
        }

        public static MobaShieldRollbackEntry FromContainer(int actorId, ShieldContainer container)
        {
            if (container == null) return Empty(actorId);

            var sourceLayers = container.Layers;
            var layers = sourceLayers == null || sourceLayers.Count == 0
                ? Array.Empty<MobaShieldLayerRollbackEntry>()
                : new MobaShieldLayerRollbackEntry[sourceLayers.Count];
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = sourceLayers[i];
                if (layer == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot capture a null Shield layer. actor={actorId} index={i}.");
                }

                layers[i] = MobaShieldLayerRollbackEntry.FromLayer(layer);
            }

            return new MobaShieldRollbackEntry(
                actorId,
                true,
                container.NextInstanceId,
                container.TotalRemaining.RawValue,
                container.Dirty,
                layers);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaShieldLayerRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int InstanceId;
        [MemoryPackOrder(1)] public readonly int ShieldId;
        [MemoryPackOrder(2)] public readonly int SourceActorId;
        [MemoryPackOrder(3)] public readonly int OwnerActorId;
        [MemoryPackOrder(4)] public readonly int TargetActorId;
        [MemoryPackOrder(5)] public readonly long SourceContextId;
        [MemoryPackOrder(6)] public readonly long RootContextId;
        [MemoryPackOrder(7)] public readonly long OwnerContextId;
        [MemoryPackOrder(8)] public readonly int SharedPoolId;
        [MemoryPackOrder(9)] public readonly int SharedPoolMemberId;
        [MemoryPackOrder(10)] public readonly bool UsesSharedPoolValue;
        [MemoryPackOrder(11)] public readonly int TransferredFromActorId;
        [MemoryPackOrder(12)] public readonly int TransferredToActorId;
        [MemoryPackOrder(13)] public readonly int TransferredAtFrame;
        [MemoryPackOrder(14)] public readonly long TransferRatioRaw;
        [MemoryPackOrder(15)] public readonly long CurrentValueRaw;
        [MemoryPackOrder(16)] public readonly long MaxValueRaw;
        [MemoryPackOrder(17)] public readonly long InitialValueRaw;
        [MemoryPackOrder(18)] public readonly long AbsorbRatioRaw;
        [MemoryPackOrder(19)] public readonly int Priority;
        [MemoryPackOrder(20)] public readonly int DamageTypeMask;
        [MemoryPackOrder(21)] public readonly int StartFrame;
        [MemoryPackOrder(22)] public readonly int ExpireFrame;
        [MemoryPackOrder(23)] public readonly bool RemoveWhenDepleted;
        [MemoryPackOrder(24)] public readonly int StackingPolicy;
        [MemoryPackOrder(25)] public readonly int ConsumePolicy;
        [MemoryPackOrder(26)] public readonly int SharePolicy;
        [MemoryPackOrder(27)] public readonly int TransferPolicy;

        public MobaShieldLayerRollbackEntry(
            int instanceId,
            int shieldId,
            int sourceActorId,
            int ownerActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            int sharedPoolId,
            int sharedPoolMemberId,
            bool usesSharedPoolValue,
            int transferredFromActorId,
            int transferredToActorId,
            int transferredAtFrame,
            long transferRatioRaw,
            long currentValueRaw,
            long maxValueRaw,
            long initialValueRaw,
            long absorbRatioRaw,
            int priority,
            int damageTypeMask,
            int startFrame,
            int expireFrame,
            bool removeWhenDepleted,
            int stackingPolicy,
            int consumePolicy,
            int sharePolicy,
            int transferPolicy)
        {
            InstanceId = instanceId;
            ShieldId = shieldId;
            SourceActorId = sourceActorId;
            OwnerActorId = ownerActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SharedPoolId = sharedPoolId;
            SharedPoolMemberId = sharedPoolMemberId;
            UsesSharedPoolValue = usesSharedPoolValue;
            TransferredFromActorId = transferredFromActorId;
            TransferredToActorId = transferredToActorId;
            TransferredAtFrame = transferredAtFrame;
            TransferRatioRaw = transferRatioRaw;
            CurrentValueRaw = currentValueRaw;
            MaxValueRaw = maxValueRaw;
            InitialValueRaw = initialValueRaw;
            AbsorbRatioRaw = absorbRatioRaw;
            Priority = priority;
            DamageTypeMask = damageTypeMask;
            StartFrame = startFrame;
            ExpireFrame = expireFrame;
            RemoveWhenDepleted = removeWhenDepleted;
            StackingPolicy = stackingPolicy;
            ConsumePolicy = consumePolicy;
            SharePolicy = sharePolicy;
            TransferPolicy = transferPolicy;
        }

        public static MobaShieldLayerRollbackEntry FromLayer(ShieldLayer layer)
        {
            return new MobaShieldLayerRollbackEntry(
                layer.InstanceId,
                layer.ShieldId,
                layer.SourceActorId,
                layer.OwnerActorId,
                layer.TargetActorId,
                layer.SourceContextId,
                layer.RootContextId,
                layer.OwnerContextId,
                layer.SharedPoolId,
                layer.SharedPoolMemberId,
                layer.UsesSharedPoolValue,
                layer.TransferredFromActorId,
                layer.TransferredToActorId,
                layer.TransferredAtFrame,
                layer.TransferRatio.RawValue,
                layer.CurrentValue.RawValue,
                layer.MaxValue.RawValue,
                layer.InitialValue.RawValue,
                layer.AbsorbRatio.RawValue,
                layer.Priority,
                layer.DamageTypeMask,
                layer.StartFrame,
                layer.ExpireFrame,
                layer.RemoveWhenDepleted,
                (int)layer.StackingPolicy,
                (int)layer.ConsumePolicy,
                (int)layer.SharePolicy,
                (int)layer.TransferPolicy);
        }

        public ShieldLayer ToLayer()
        {
            return new ShieldLayer
            {
                InstanceId = InstanceId,
                ShieldId = ShieldId,
                SourceActorId = SourceActorId,
                OwnerActorId = OwnerActorId,
                TargetActorId = TargetActorId,
                SourceContextId = SourceContextId,
                RootContextId = RootContextId,
                OwnerContextId = OwnerContextId,
                SharedPoolId = SharedPoolId,
                SharedPoolMemberId = SharedPoolMemberId,
                UsesSharedPoolValue = UsesSharedPoolValue,
                TransferredFromActorId = TransferredFromActorId,
                TransferredToActorId = TransferredToActorId,
                TransferredAtFrame = TransferredAtFrame,
                TransferRatio = Fixed64.FromRaw(TransferRatioRaw),
                CurrentValue = Fixed64.FromRaw(CurrentValueRaw),
                MaxValue = Fixed64.FromRaw(MaxValueRaw),
                InitialValue = Fixed64.FromRaw(InitialValueRaw),
                AbsorbRatio = Fixed64.FromRaw(AbsorbRatioRaw),
                Priority = Priority,
                DamageTypeMask = DamageTypeMask,
                StartFrame = StartFrame,
                ExpireFrame = ExpireFrame,
                RemoveWhenDepleted = RemoveWhenDepleted,
                StackingPolicy = (ShieldStackingPolicy)StackingPolicy,
                ConsumePolicy = (ShieldConsumePolicy)ConsumePolicy,
                SharePolicy = (ShieldSharePolicy)SharePolicy,
                TransferPolicy = (ShieldTransferPolicy)TransferPolicy,
            };
        }
    }
}
