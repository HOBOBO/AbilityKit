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
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaShieldRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10007;
        // v2 (2026-08-15): 护盾数值字段定点化（Q32.32），快照以 raw long 存储（TotalRemaining/TransferRatio/CurrentValue/MaxValue/InitialValue/AbsorbRatio）。
        internal const int CurrentPayloadVersion = 3;

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
                if (!_shields.TryGetContainerMutable(actorId, out var container))
                {
                    entries.Add(MobaShieldRollbackEntry.Empty(actorId));
                    continue;
                }

                entries.Add(MobaShieldRollbackEntry.FromContainer(actorId, container));
            }

            entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
            ValidateEntries(entries);
            var pools = new List<MobaSharedShieldPoolRollbackEntry>();
            foreach (var pair in _shields.GetSharedPoolsMutable())
            {
                if (pair.Value == null) continue;
                pools.Add(MobaSharedShieldPoolRollbackEntry.FromPool(pair.Value));
            }
            pools.Sort((a, b) => a.PoolId.CompareTo(b.PoolId));
            return MemoryPackSerializer.Serialize(new MobaShieldRollbackPayload(
                CurrentPayloadVersion,
                entries.Count == 0 ? Array.Empty<MobaShieldRollbackEntry>() : entries.ToArray(),
                pools.Count == 0 ? Array.Empty<MobaSharedShieldPoolRollbackEntry>() : pools.ToArray()));
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
            var pools = BuildPools(snapshot.Pools ?? Array.Empty<MobaSharedShieldPoolRollbackEntry>(), out var nextPoolId);
            _shields.RestoreSharedPools(pools, nextPoolId);
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

        private static Dictionary<int, SharedShieldPool> BuildPools(
            IReadOnlyList<MobaSharedShieldPoolRollbackEntry> entries, out int nextPoolId)
        {
            var restored = new Dictionary<int, SharedShieldPool>();
            nextPoolId = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.PoolId <= 0 || restored.ContainsKey(entry.PoolId))
                    throw new InvalidOperationException($"Shared shield pool identities must be unique. poolId={entry.PoolId}.");
                restored.Add(entry.PoolId, entry.ToPool());
                if (entry.PoolId > nextPoolId) nextPoolId = entry.PoolId;
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
    public readonly partial struct MobaSharedShieldPoolRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int PoolId;
        [MemoryPackOrder(1)] public readonly int ShieldId;
        [MemoryPackOrder(2)] public readonly int SourceActorId;
        [MemoryPackOrder(3)] public readonly int OwnerActorId;
        [MemoryPackOrder(4)] public readonly long SourceContextId;
        [MemoryPackOrder(5)] public readonly long RootContextId;
        [MemoryPackOrder(6)] public readonly long OwnerContextId;
        [MemoryPackOrder(7)] public readonly long CurrentValueRaw;
        [MemoryPackOrder(8)] public readonly long MaxValueRaw;
        [MemoryPackOrder(9)] public readonly long InitialValueRaw;
        [MemoryPackOrder(10)] public readonly long AbsorbRatioRaw;
        [MemoryPackOrder(11)] public readonly int Priority;
        [MemoryPackOrder(12)] public readonly int DamageTypeMask;
        [MemoryPackOrder(13)] public readonly int StartFrame;
        [MemoryPackOrder(14)] public readonly int ExpireFrame;
        [MemoryPackOrder(15)] public readonly bool RemoveWhenDepleted;
        [MemoryPackOrder(16)] public readonly int StackingPolicy;
        [MemoryPackOrder(17)] public readonly int ConsumePolicy;
        [MemoryPackOrder(18)] public readonly int SharePolicy;
        [MemoryPackOrder(19)] public readonly int TransferPolicy;
        [MemoryPackOrder(20)] public readonly int Version;
        [MemoryPackOrder(21)] public readonly int NextMemberId;
        [MemoryPackOrder(22)] public readonly MobaSharedShieldMemberRollbackEntry[] Members;

        [MemoryPackConstructor]
        public MobaSharedShieldPoolRollbackEntry(int poolId, int shieldId, int sourceActorId, int ownerActorId,
            long sourceContextId, long rootContextId, long ownerContextId, long currentValueRaw, long maxValueRaw,
            long initialValueRaw, long absorbRatioRaw, int priority, int damageTypeMask, int startFrame, int expireFrame,
            bool removeWhenDepleted, int stackingPolicy, int consumePolicy, int sharePolicy, int transferPolicy,
            int version, int nextMemberId, MobaSharedShieldMemberRollbackEntry[] members)
        {
            PoolId = poolId; ShieldId = shieldId; SourceActorId = sourceActorId; OwnerActorId = ownerActorId;
            SourceContextId = sourceContextId; RootContextId = rootContextId; OwnerContextId = ownerContextId;
            CurrentValueRaw = currentValueRaw; MaxValueRaw = maxValueRaw; InitialValueRaw = initialValueRaw;
            AbsorbRatioRaw = absorbRatioRaw; Priority = priority; DamageTypeMask = damageTypeMask;
            StartFrame = startFrame; ExpireFrame = expireFrame; RemoveWhenDepleted = removeWhenDepleted;
            StackingPolicy = stackingPolicy; ConsumePolicy = consumePolicy; SharePolicy = sharePolicy;
            TransferPolicy = transferPolicy; Version = version; NextMemberId = nextMemberId; Members = members;
        }

        public static MobaSharedShieldPoolRollbackEntry FromPool(SharedShieldPool pool)
        {
            var source = pool.Members ?? new List<SharedShieldPoolMember>();
            var members = new MobaSharedShieldMemberRollbackEntry[source.Count];
            for (var i = 0; i < source.Count; i++) members[i] = MobaSharedShieldMemberRollbackEntry.FromMember(source[i]);
            return new MobaSharedShieldPoolRollbackEntry(pool.PoolId, pool.ShieldId, pool.SourceActorId, pool.OwnerActorId,
                pool.SourceContextId, pool.RootContextId, pool.OwnerContextId, pool.CurrentValue.RawValue,
                pool.MaxValue.RawValue, pool.InitialValue.RawValue, pool.AbsorbRatio.RawValue, pool.Priority,
                pool.DamageTypeMask, pool.StartFrame, pool.ExpireFrame, pool.RemoveWhenDepleted,
                (int)pool.StackingPolicy, (int)pool.ConsumePolicy, (int)pool.SharePolicy, (int)pool.TransferPolicy,
                pool.Version, pool.NextMemberId, members);
        }

        public SharedShieldPool ToPool()
        {
            var source = Members ?? Array.Empty<MobaSharedShieldMemberRollbackEntry>();
            var members = new List<SharedShieldPoolMember>(source.Length);
            for (var i = 0; i < source.Length; i++) members.Add(source[i].ToMember());
            return new SharedShieldPool
            {
                PoolId = PoolId, ShieldId = ShieldId, SourceActorId = SourceActorId, OwnerActorId = OwnerActorId,
                SourceContextId = SourceContextId, RootContextId = RootContextId, OwnerContextId = OwnerContextId,
                CurrentValue = Fixed64.FromRaw(CurrentValueRaw), MaxValue = Fixed64.FromRaw(MaxValueRaw),
                InitialValue = Fixed64.FromRaw(InitialValueRaw), AbsorbRatio = Fixed64.FromRaw(AbsorbRatioRaw),
                Priority = Priority, DamageTypeMask = DamageTypeMask, StartFrame = StartFrame, ExpireFrame = ExpireFrame,
                RemoveWhenDepleted = RemoveWhenDepleted, StackingPolicy = (ShieldStackingPolicy)StackingPolicy,
                ConsumePolicy = (ShieldConsumePolicy)ConsumePolicy, SharePolicy = (ShieldSharePolicy)SharePolicy,
                TransferPolicy = (ShieldTransferPolicy)TransferPolicy, Version = Version, NextMemberId = NextMemberId,
                Members = members,
            };
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSharedShieldMemberRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int MemberId;
        [MemoryPackOrder(1)] public readonly int ActorId;
        [MemoryPackOrder(2)] public readonly long WeightRaw;
        [MemoryPackOrder(3)] public readonly long MaxConsumeValueRaw;
        [MemoryPackOrder(4)] public readonly int JoinedFrame;
        [MemoryPackOrder(5)] public readonly int LeftFrame;
        [MemoryPackOrder(6)] public readonly bool Active;
        [MemoryPackOrder(7)] public readonly long ConsumedValueRaw;

        public MobaSharedShieldMemberRollbackEntry(int memberId, int actorId, long weightRaw, long maxConsumeValueRaw,
            int joinedFrame, int leftFrame, bool active, long consumedValueRaw = 0L)
        {
            MemberId = memberId; ActorId = actorId; WeightRaw = weightRaw; MaxConsumeValueRaw = maxConsumeValueRaw;
            JoinedFrame = joinedFrame; LeftFrame = leftFrame; Active = active; ConsumedValueRaw = consumedValueRaw;
        }

        public static MobaSharedShieldMemberRollbackEntry FromMember(SharedShieldPoolMember member)
            => new MobaSharedShieldMemberRollbackEntry(member.MemberId, member.ActorId, member.Weight.RawValue,
                member.MaxConsumeValue.RawValue, member.JoinedFrame, member.LeftFrame, member.Active, member.ConsumedValue.RawValue);

        public SharedShieldPoolMember ToMember() => new SharedShieldPoolMember
        {
            MemberId = MemberId, ActorId = ActorId, Weight = Fixed64.FromRaw(WeightRaw),
            MaxConsumeValue = Fixed64.FromRaw(MaxConsumeValueRaw), JoinedFrame = JoinedFrame,
            LeftFrame = LeftFrame, Active = Active, ConsumedValue = Fixed64.FromRaw(ConsumedValueRaw),
        };
    }

    [MemoryPackable]
    public readonly partial struct MobaShieldRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaShieldRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaShieldRollbackPayload(int version, MobaShieldRollbackEntry[] entries, MobaSharedShieldPoolRollbackEntry[] pools)
        {
            Version = version;
            Entries = entries;
            Pools = pools;
        }

        [MemoryPackOrder(2)] public readonly MobaSharedShieldPoolRollbackEntry[] Pools;
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
