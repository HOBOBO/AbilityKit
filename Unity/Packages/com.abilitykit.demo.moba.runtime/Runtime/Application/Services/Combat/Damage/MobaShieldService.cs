using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaShieldService))]
    public sealed class MobaShieldService : IService
    {
        private static readonly Dictionary<int, SharedShieldPool> EmptySharedPools = new Dictionary<int, SharedShieldPool>();
        private Dictionary<int, ShieldContainer> _containers = new Dictionary<int, ShieldContainer>();
        // Shared state is kept outside actor containers. A binding in a container only
        // identifies the pool/member; the pool owns the authoritative balance.
        private Dictionary<int, SharedShieldPool> _sharedPools;
        private Dictionary<int, List<int>> _actorPoolIds;
        private int _nextPoolId;
        private List<int> _poolCleanupIds;
        private List<int> _targetScratch;
        private HashSet<int> _targetScratchSet;
        private readonly IFrameTime _frameTime;

        private Dictionary<int, SharedShieldPool> SharedPools =>
            _sharedPools ?? (_sharedPools = new Dictionary<int, SharedShieldPool>());

        private Dictionary<int, List<int>> ActorPoolIds =>
            _actorPoolIds ?? (_actorPoolIds = new Dictionary<int, List<int>>());

        public MobaShieldService(IFrameTime frameTime = null)
        {
            _frameTime = frameTime;
        }

        public int AddShield(int targetActorId, ShieldLayer layer)
        {
            if (targetActorId <= 0) return 0;
            if (layer == null) return 0;

            if (layer.SharePolicy != ShieldSharePolicy.None)
            {
                var targets = new[] { targetActorId };
                return AddSharedShield(targets, layer);
            }

            var container = GetOrCreateContainer(targetActorId);
            if (container.Layers == null) container.Layers = new List<ShieldLayer>();

            NormalizeLayer(targetActorId, layer);

            var existing = FindStackTarget(container, layer);
            if (existing != null)
            {
                ApplyStacking(existing, layer);
                Recalculate(container);
                return existing.InstanceId;
            }

            if (layer.StackingPolicy == ShieldStackingPolicy.ReplaceLowerPriority)
            {
                container.Layers.RemoveAll(x => x != null && x.ShieldId == layer.ShieldId && x.SourceActorId == layer.SourceActorId && x.Priority <= layer.Priority);
            }

            var instanceId = layer.InstanceId > 0 ? layer.InstanceId : ++container.NextInstanceId;
            layer.InstanceId = instanceId;
            container.Layers.Add(layer);
            Recalculate(container);
            return instanceId;
        }

        /// <summary>
        /// Creates one shared pool and binds every target to it. This is the only API
        /// that creates shared balance; actor bindings never own a second balance.
        /// </summary>
        public int AddSharedShield(IReadOnlyList<int> targetActorIds, ShieldLayer template)
        {
            if (targetActorIds == null || targetActorIds.Count == 0 || template == null ||
                template.SharePolicy == ShieldSharePolicy.None) return 0;

            if (_targetScratch == null) _targetScratch = new List<int>(Math.Min(targetActorIds.Count, 8));
            if (_targetScratchSet == null) _targetScratchSet = new HashSet<int>();
            var targets = _targetScratch;
            var targetSet = _targetScratchSet;
            targets.Clear();
            targetSet.Clear();
            try
            {
                for (var i = 0; i < targetActorIds.Count; i++)
                {
                    var actorId = targetActorIds[i];
                    if (actorId > 0 && targetSet.Add(actorId)) targets.Add(actorId);
                }
                if (targets.Count == 0) return 0;

                NormalizeLayer(targets[0], template);
                var pool = new SharedShieldPool
                {
                    PoolId = ++_nextPoolId,
                    ShieldId = template.ShieldId,
                    SourceActorId = template.SourceActorId,
                    OwnerActorId = template.OwnerActorId,
                    SourceContextId = template.SourceContextId,
                    RootContextId = template.RootContextId,
                    OwnerContextId = template.OwnerContextId,
                    Members = new List<SharedShieldPoolMember>(targets.Count),
                    CurrentValue = template.CurrentValue,
                    MaxValue = template.MaxValue,
                    InitialValue = template.InitialValue,
                    AbsorbRatio = template.AbsorbRatio,
                    Priority = template.Priority,
                    DamageTypeMask = template.DamageTypeMask,
                    StartFrame = template.StartFrame,
                    ExpireFrame = template.ExpireFrame,
                    RemoveWhenDepleted = template.RemoveWhenDepleted,
                    StackingPolicy = template.StackingPolicy,
                    ConsumePolicy = template.ConsumePolicy,
                    SharePolicy = template.SharePolicy,
                    TransferPolicy = template.TransferPolicy,
                    Version = 0,
                    NextMemberId = 0,
                };
                SharedPools.Add(pool.PoolId, pool);

                var firstBinding = 0;
                for (var i = 0; i < targets.Count; i++)
                {
                    var binding = BindPool(pool, targets[i], pool.StartFrame, Fixed64.One, Fixed64.Zero);
                    if (i == 0) firstBinding = binding;
                }
                return firstBinding;
            }
            finally
            {
                targets.Clear();
                targetSet.Clear();
            }
        }

        public Fixed64 Absorb(AttackInfo attack, Fixed64 incomingDamage)
        {
            var plan = PreviewAbsorb(attack, incomingDamage);
            if (!CommitAbsorb(plan)) return Fixed64.Zero;
            FinalizeAbsorb(plan);
            return plan.Absorbed;
        }

        public ShieldAbsorbPlan PreviewAbsorb(AttackInfo attack, Fixed64 incomingDamage)
        {
            var currentFrame = _frameTime != null ? _frameTime.Frame.Value : int.MinValue;
            return PreviewAbsorb(attack, incomingDamage, currentFrame);
        }

        public ShieldAbsorbPlan PreviewAbsorb(AttackInfo attack, Fixed64 incomingDamage, int currentFrame)
        {
            var plan = new ShieldAbsorbPlan(attack != null ? attack.TargetActorId : 0);
            if (attack == null || incomingDamage <= Fixed64.Zero) return plan;
            if (!_containers.TryGetValue(attack.TargetActorId, out var container) || container == null) return plan;
            if (container.Layers == null || container.Layers.Count == 0) return plan;

            var remainingDamage = incomingDamage;
            for (var i = 0; i < container.Layers.Count && remainingDamage > Fixed64.Zero; i++)
            {
                var layer = container.Layers[i];
                if (!CanAbsorb(layer, attack.DamageType, currentFrame)) continue;

                var ratio = layer.AbsorbRatio <= Fixed64.Zero ? Fixed64.One : DeterministicMath.Min(Fixed64.One, layer.AbsorbRatio);
                var available = GetEffectiveCurrentValue(layer);
                var take = DeterministicMath.Min(available, remainingDamage * ratio);
                if (take <= Fixed64.Zero) continue;

                if (layer.UsesSharedPoolValue && layer.SharedPoolId > 0 &&
                    TryGetSharedPoolState(layer.SharedPoolId, out var pool))
                {
                    var projected = plan.GetProjectedPoolValue(pool.PoolId, pool.CurrentValue);
                    take = DeterministicMath.Min(projected, remainingDamage * ratio);
                    take = DeterministicMath.Min(take, GetMemberCapacity(pool, layer.SharedPoolMemberId, projected));
                    if (take <= Fixed64.Zero) continue;
                    plan.AddPool(layer.InstanceId, layer.SharedPoolId, layer.SharedPoolMemberId,
                        pool.Version, pool.CurrentValue, take);
                }
                else
                {
                    plan.AddLayer(layer.InstanceId, layer.CurrentValue, take);
                }
                remainingDamage -= take;
            }
            return plan;
        }

        public bool CommitAbsorb(ShieldAbsorbPlan plan)
        {
            if (plan == null || plan.Absorbed <= Fixed64.Zero) return plan != null;
            if (!_containers.TryGetValue(plan.TargetActorId, out var container) || container == null) return false;
            if (container.Layers == null) return false;

            Dictionary<int, Fixed64> poolDeltas = null;
            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                var layer = FindLayer(container, entry.InstanceId);
                if (layer == null) return false;
                if (entry.PoolId > 0)
                {
                    if (!layer.UsesSharedPoolValue || layer.SharedPoolId != entry.PoolId ||
                        !TryGetSharedPoolState(entry.PoolId, out var pool) ||
                        pool.Version != entry.ExpectedPoolVersion || pool.CurrentValue != entry.OldPoolValue ||
                        !IsMemberActive(pool, entry.PoolMemberId, CurrentFrame())) return false;
                    if (poolDeltas == null) poolDeltas = new Dictionary<int, Fixed64>(2);
                    if (poolDeltas.TryGetValue(entry.PoolId, out var existing)) poolDeltas[entry.PoolId] = existing + entry.ConsumedValue;
                    else poolDeltas.Add(entry.PoolId, entry.ConsumedValue);
                }
                else if (layer.CurrentValue != entry.OldValue) return false;
            }

            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                if (entry.PoolId == 0)
                    FindLayer(container, entry.InstanceId).CurrentValue = entry.OldValue - entry.ConsumedValue;
            }
            if (poolDeltas != null)
            {
                foreach (var pair in poolDeltas)
                {
                    var pool = SharedPools[pair.Key];
                    pool.CurrentValue -= pair.Value;
                    pool.Version++;
                    for (var i = 0; i < plan.Entries.Count; i++)
                    {
                        var entry = plan.Entries[i];
                        if (entry.PoolId != pair.Key) continue;
                        var member = FindMember(pool, entry.PoolMemberId);
                        if (member != null) member.ConsumedValue += entry.ConsumedValue;
                    }
                    RefreshPoolBindings(pool);
                }
            }
            Recalculate(container);
            plan.MarkCommitted();
            return true;
        }

        public void RollbackAbsorb(ShieldAbsorbPlan plan)
        {
            if (plan == null || plan.Absorbed <= Fixed64.Zero) return;
            if (!plan.IsCommitted) return;
            if (!_containers.TryGetValue(plan.TargetActorId, out var container) || container == null) return;
            if (container.Layers == null) return;

            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                var layer = FindLayer(container, entry.InstanceId);
                if (layer == null) continue;
                if (entry.PoolId > 0)
                {
                    var shouldRestore = true;
                    for (var prior = 0; prior < i; prior++)
                    {
                        if (plan.Entries[prior].PoolId != entry.PoolId) continue;
                        shouldRestore = false;
                        break;
                    }

                    if (shouldRestore && TryGetSharedPoolState(entry.PoolId, out var pool) &&
                        pool.Version == entry.ExpectedPoolVersion + 1)
                    {
                        pool.CurrentValue = entry.OldPoolValue;
                        pool.Version = entry.ExpectedPoolVersion;
                        for (var j = 0; j < plan.Entries.Count; j++)
                        {
                            var consumed = plan.Entries[j];
                            if (consumed.PoolId != entry.PoolId) continue;
                            var member = FindMember(pool, consumed.PoolMemberId);
                            if (member != null) member.ConsumedValue -= consumed.ConsumedValue;
                        }
                        RefreshPoolBindings(pool);
                    }
                }
                else layer.CurrentValue = entry.OldValue;
            }
            Recalculate(container);
            plan.MarkRolledBack();
        }

        public void FinalizeAbsorb(ShieldAbsorbPlan plan)
        {
            if (plan == null || plan.Absorbed <= Fixed64.Zero) return;
            if (!_containers.TryGetValue(plan.TargetActorId, out var container) || container == null) return;

            RemoveDepleted(container);
            RemoveDepletedSharedPools();
            Recalculate(container);
        }

        public Fixed64 GetTotalRemainingFixed(int targetActorId)
        {
            return _containers.TryGetValue(targetActorId, out var container) && container != null ? container.TotalRemaining : Fixed64.Zero;
        }

        public float GetTotalRemaining(int targetActorId)
        {
            return MobaResourceFixedConvert.ToSingle(GetTotalRemainingFixed(targetActorId));
        }

        /// <summary>
        /// Returns a value-only projection for presentation and diagnostics. The projection
        /// reads shared balances from their pools and never exposes the mutable container.
        /// Callers must treat an absent container as an empty snapshot.
        /// </summary>
        public ShieldPresentationSnapshot GetPresentationSnapshot(int targetActorId)
        {
            if (targetActorId <= 0 || !_containers.TryGetValue(targetActorId, out var container) ||
                container == null || container.Layers == null || container.Layers.Count == 0)
            {
                return new ShieldPresentationSnapshot(
                    targetActorId,
                    Fixed64.Zero,
                    Fixed64.Zero,
                    Fixed64.Zero,
                    Fixed64.Zero,
                    0,
                    0,
                    0,
                    0,
                    false);
            }

            var personal = Fixed64.Zero;
            var shared = Fixed64.Zero;
            var capacity = Fixed64.Zero;
            var primaryShieldId = 0;
            var primaryPoolId = 0;
            var primaryPoolVersion = 0;
            var activeCount = 0;
            var hasShared = false;
            var currentFrame = CurrentFrame();

            for (var i = 0; i < container.Layers.Count; i++)
            {
                var layer = container.Layers[i];
                if (layer == null) continue;
                if (currentFrame != int.MinValue && layer.StartFrame > 0 && currentFrame < layer.StartFrame) continue;
                if (currentFrame != int.MinValue && layer.ExpireFrame > 0 && currentFrame >= layer.ExpireFrame) continue;

                var current = GetEffectiveCurrentValue(layer);
                if (current <= Fixed64.Zero) continue;

                activeCount++;
                if (primaryShieldId == 0)
                {
                    primaryShieldId = layer.ShieldId;
                    primaryPoolId = layer.UsesSharedPoolValue ? layer.SharedPoolId : 0;
                    if (primaryPoolId > 0 && TryGetSharedPoolState(primaryPoolId, out var primaryPool))
                        primaryPoolVersion = primaryPool.Version;
                }

                if (layer.UsesSharedPoolValue)
                {
                    shared += current;
                    hasShared = true;
                    if (TryGetSharedPoolState(layer.SharedPoolId, out var pool))
                        capacity += pool.MaxValue;
                }
                else
                {
                    personal += current;
                    capacity += layer.MaxValue;
                }
            }

            return new ShieldPresentationSnapshot(
                targetActorId,
                personal,
                shared,
                personal + shared,
                capacity,
                primaryShieldId,
                primaryPoolId,
                primaryPoolVersion,
                activeCount,
                hasShared);
        }

        public bool TryGetContainer(int targetActorId, out ShieldContainerView view)
        {
            if (!_containers.TryGetValue(targetActorId, out var container) || container == null)
            {
                view = default;
                return false;
            }

            view = new ShieldContainerView(
                container.Layers?.Count ?? 0,
                container.NextInstanceId,
                container.TotalRemaining,
                container.Dirty);
            return true;
        }

        public int GetLayerCount(int targetActorId)
        {
            return _containers.TryGetValue(targetActorId, out var container) && container != null
                ? container.Layers?.Count ?? 0
                : 0;
        }

        public bool TryGetSharedPool(int poolId, out SharedShieldPoolView view)
        {
            if (!TryGetSharedPoolState(poolId, out var pool))
            {
                view = default;
                return false;
            }

            var active = 0;
            var members = pool.Members;
            if (members == null)
            {
                view = new SharedShieldPoolView(pool.PoolId, pool.ShieldId, pool.CurrentValue, pool.MaxValue,
                    pool.Version, 0, 0, pool.StartFrame, pool.ExpireFrame);
                return true;
            }
            for (var i = 0; i < members.Count; i++) if (members[i] != null && members[i].Active) active++;
            view = new SharedShieldPoolView(pool.PoolId, pool.ShieldId, pool.CurrentValue, pool.MaxValue,
                pool.Version, members.Count, active, pool.StartFrame, pool.ExpireFrame);
            return true;
        }

        public int GetSharedPoolIdForActor(int actorId, int shieldId = 0)
        {
            if (_actorPoolIds == null || !_actorPoolIds.TryGetValue(actorId, out var poolIds) || poolIds == null) return 0;
            for (var i = poolIds.Count - 1; i >= 0; i--)
            {
                if (!TryGetSharedPoolState(poolIds[i], out var pool)) continue;
                if (shieldId <= 0 || pool.ShieldId == shieldId) return pool.PoolId;
            }
            return 0;
        }

        public bool JoinSharedPool(int poolId, int actorId, int frame, Fixed64 weight = default, Fixed64 maxConsumeValue = default)
        {
            if (!TryGetSharedPoolState(poolId, out var pool) || actorId <= 0) return false;
            return BindPool(pool, actorId, frame, weight <= Fixed64.Zero ? Fixed64.One : weight, maxConsumeValue) > 0;
        }

        public bool LeaveSharedPool(int poolId, int actorId, int frame)
        {
            if (!TryGetSharedPoolState(poolId, out var pool) || pool.Members == null) return false;
            var changed = false;
            for (var i = 0; i < pool.Members.Count; i++)
            {
                var member = pool.Members[i];
                if (member == null || member.ActorId != actorId || !member.Active) continue;
                member.Active = false;
                member.LeftFrame = frame;
                changed = true;
            }
            if (!changed) return false;
            RemovePoolBindings(actorId, poolId);
            pool.Version++;
            return true;
        }

        /// <summary>Rebinds a personal or shared binding to another actor with an explicit transfer policy.</summary>
        public bool TransferShield(int sourceActorId, int instanceId, int targetActorId, int frame,
            ShieldTransferPolicy policy = ShieldTransferPolicy.MoveRemaining, Fixed64 ratio = default)
        {
            if (sourceActorId <= 0 || targetActorId <= 0 || sourceActorId == targetActorId || instanceId <= 0) return false;
            if (!_containers.TryGetValue(sourceActorId, out var source) || source?.Layers == null) return false;
            var layer = FindLayer(source, instanceId);
            if (layer == null) return false;
            var transferRatio = ratio <= Fixed64.Zero ? Fixed64.One : DeterministicMath.Min(Fixed64.One, ratio);

            if (layer.UsesSharedPoolValue && layer.SharedPoolId > 0)
            {
                if (!TryGetSharedPoolState(layer.SharedPoolId, out var pool)) return false;
                if (policy == ShieldTransferPolicy.CopySnapshot)
                {
                    var snapshotValue = pool.CurrentValue * transferRatio;
                    var snapshot = CloneLayer(layer, targetActorId, snapshotValue, sourceActorId, targetActorId, frame);
                    return snapshotValue > Fixed64.Zero && AddShield(targetActorId, snapshot) > 0;
                }
                if (policy == ShieldTransferPolicy.SplitRemaining)
                {
                    var splitValue = pool.CurrentValue * transferRatio;
                    if (splitValue <= Fixed64.Zero) return false;
                    pool.CurrentValue -= splitValue;
                    pool.Version++;
                    RefreshPoolBindings(pool);
                    var split = CloneLayer(layer, targetActorId, splitValue, sourceActorId, targetActorId, frame);
                    if (AddShield(targetActorId, split) > 0) return true;
                    pool.CurrentValue += splitValue;
                    pool.Version++;
                    RefreshPoolBindings(pool);
                    return false;
                }
                var sourceMember = FindMember(pool, layer.SharedPoolMemberId);
                if (sourceMember == null || !sourceMember.Active || !CanBindPool(pool, targetActorId)) return false;
                if (!LeaveSharedPool(pool.PoolId, sourceActorId, frame)) return false;
                if (BindPool(pool, targetActorId, frame, sourceMember.Weight, sourceMember.MaxConsumeValue) <= 0)
                {
                    BindPool(pool, sourceActorId, frame, sourceMember.Weight, sourceMember.MaxConsumeValue);
                    return false;
                }
                if (_containers.TryGetValue(targetActorId, out var targetContainer) && targetContainer?.Layers != null)
                {
                    for (var i = 0; i < targetContainer.Layers.Count; i++)
                    {
                        var transferred = targetContainer.Layers[i];
                        if (transferred == null || transferred.SharedPoolId != pool.PoolId) continue;
                        transferred.TransferredFromActorId = sourceActorId;
                        transferred.TransferredToActorId = targetActorId;
                        transferred.TransferredAtFrame = frame;
                        break;
                    }
                }
                RemovePoolBindings(sourceActorId, pool.PoolId);
                Recalculate(source);
                return true;
            }

            var remaining = layer.CurrentValue * transferRatio;
            if (remaining <= Fixed64.Zero) return false;
            var copy = CloneLayer(layer, targetActorId, remaining, sourceActorId, targetActorId, frame);
            if (AddShield(targetActorId, copy) <= 0) return false;
            if (policy == ShieldTransferPolicy.MoveRemaining || policy == ShieldTransferPolicy.SplitRemaining)
                layer.CurrentValue -= remaining;
            if (policy == ShieldTransferPolicy.MoveRemaining) source.Layers.Remove(layer);
            Recalculate(source);
            return true;
        }

        public bool RemoveSharedPool(int poolId)
        {
            if (!TryGetSharedPoolState(poolId, out var pool)) return false;
            if (pool.Members != null)
            {
                for (var i = 0; i < pool.Members.Count; i++)
                {
                    var member = pool.Members[i];
                    if (member != null) RemovePoolBindings(member.ActorId, poolId);
                }
            }
            _sharedPools.Remove(poolId);
            return true;
        }

        internal bool TryGetContainerMutable(int targetActorId, out ShieldContainer container)
        {
            return _containers.TryGetValue(targetActorId, out container) && container != null;
        }

        public bool RemoveActor(int actorId)
        {
            if (actorId <= 0) return false;
            var removed = _containers.Remove(actorId);
            if (_actorPoolIds != null && _actorPoolIds.TryGetValue(actorId, out var poolIds))
            {
                while (poolIds.Count > 0)
                    LeaveSharedPool(poolIds[poolIds.Count - 1], actorId, CurrentFrame());
                _actorPoolIds.Remove(actorId);
                removed = true;
            }
            return removed;
        }

        internal void RestoreContainers(Dictionary<int, ShieldContainer> containers)
        {
            if (containers == null) throw new ArgumentNullException(nameof(containers));

            foreach (var pair in containers)
            {
                if (pair.Key <= 0 || pair.Value == null)
                {
                    throw new ArgumentException("Shield restore state contains an invalid container.", nameof(containers));
                }
            }

            _containers = containers;
        }

        internal Dictionary<int, SharedShieldPool> GetSharedPoolsMutable() => _sharedPools ?? EmptySharedPools;

        internal void RestoreSharedPools(Dictionary<int, SharedShieldPool> pools, int nextPoolId)
        {
            _sharedPools = pools ?? throw new ArgumentNullException(nameof(pools));
            _nextPoolId = nextPoolId;
            _actorPoolIds = null;
            if (_sharedPools.Count == 0)
            {
                _sharedPools = null;
                return;
            }
            foreach (var pair in _sharedPools)
            {
                var pool = pair.Value;
                if (pool == null) continue;
                if (pool.Members == null) pool.Members = new List<SharedShieldPoolMember>();
                for (var i = 0; i < pool.Members.Count; i++)
                {
                    var member = pool.Members[i];
                    if (member != null && member.Active) TrackPoolActor(member.ActorId, pool.PoolId);
                }
            }
        }

        public bool RemoveShield(int targetActorId, int instanceId)
        {
            if (targetActorId <= 0 || instanceId <= 0) return false;
            if (!_containers.TryGetValue(targetActorId, out var container) || container == null) return false;
            if (container.Layers == null) return false;

            var layer = FindLayer(container, instanceId);
            var detached = false;
            if (layer != null && layer.UsesSharedPoolValue && layer.SharedPoolId > 0)
                detached = LeaveSharedPool(layer.SharedPoolId, targetActorId, CurrentFrame());
            var removed = container.Layers.RemoveAll(x => x != null && x.InstanceId == instanceId) > 0;
            removed |= detached;
            if (removed) Recalculate(container);
            return removed;
        }

        public int RemoveShields(int targetActorId, int shieldId, int sourceActorId, bool removeAll)
        {
            if (targetActorId <= 0) return 0;
            if (shieldId <= 0 && sourceActorId <= 0) return 0;
            if (!_containers.TryGetValue(targetActorId, out var container) || container == null) return 0;
            if (container.Layers == null || container.Layers.Count == 0) return 0;

            var removed = 0;
            for (var i = container.Layers.Count - 1; i >= 0; i--)
            {
                var layer = container.Layers[i];
                if (layer == null) continue;
                if (shieldId > 0 && layer.ShieldId != shieldId) continue;
                if (sourceActorId > 0 && layer.SourceActorId != sourceActorId) continue;

                if (layer.UsesSharedPoolValue && layer.SharedPoolId > 0)
                {
                    var poolId = layer.SharedPoolId;
                    if (LeaveSharedPool(poolId, targetActorId, CurrentFrame()))
                    {
                        removed++;
                        if (!removeAll) break;
                        i = container.Layers.Count;
                        continue;
                    }
                }
                if (i < container.Layers.Count)
                {
                    container.Layers.RemoveAt(i);
                    removed++;
                }
                if (!removeAll) break;
            }

            if (removed > 0) Recalculate(container);
            return removed;
        }

        public int CleanupExpired(int currentFrame)
        {
            if (currentFrame <= 0) return 0;

            var removed = 0;
            foreach (var pair in _containers)
            {
                var container = pair.Value;
                if (container == null) continue;
                if (container.Layers == null || container.Layers.Count == 0) continue;

                var before = container.Layers.Count;
                container.Layers.RemoveAll(x => x == null || IsExpired(x, currentFrame) ||
                    (x.RemoveWhenDepleted && GetEffectiveCurrentValue(x) <= Fixed64.Zero));
                var delta = before - container.Layers.Count;
                if (delta <= 0) continue;

                removed += delta;
                Recalculate(container);
            }

            RemoveExpiredPoolBindings(currentFrame);
            RemoveDepletedSharedPools(currentFrame);
            return removed;
        }

        private void RemoveExpiredPoolBindings(int currentFrame)
        {
            if (_sharedPools != null)
            {
                foreach (var pair in _sharedPools)
                {
                    var pool = pair.Value;
                    if (pool == null || pool.Members == null) continue;
                    if (pool.ExpireFrame <= 0 || currentFrame < pool.ExpireFrame) continue;
                    for (var i = 0; i < pool.Members.Count; i++)
                    {
                        var member = pool.Members[i];
                        if (member == null || !member.Active) continue;
                        member.Active = false;
                        member.LeftFrame = currentFrame;
                        RemovePoolBindings(member.ActorId, pool.PoolId);
                    }
                    pool.Version++;
                }
            }
        }

        private ShieldContainer GetOrCreateContainer(int targetActorId)
        {
            if (_containers.TryGetValue(targetActorId, out var container) && container != null) return container;

            container = new ShieldContainer
            {
                Layers = new List<ShieldLayer>(),
                NextInstanceId = 0,
                TotalRemaining = Fixed64.Zero,
                Dirty = false,
            };
            _containers[targetActorId] = container;
            return container;
        }

        private int CurrentFrame()
        {
            return _frameTime != null ? _frameTime.Frame.Value : int.MinValue;
        }

        private bool TryGetSharedPoolState(int poolId, out SharedShieldPool pool)
        {
            if (_sharedPools != null && _sharedPools.TryGetValue(poolId, out pool) && pool != null) return true;
            pool = null;
            return false;
        }

        private Fixed64 GetEffectiveCurrentValue(ShieldLayer layer)
        {
            if (layer == null) return Fixed64.Zero;
            if (layer.UsesSharedPoolValue && layer.SharedPoolId > 0 &&
                TryGetSharedPoolState(layer.SharedPoolId, out var pool))
                return pool.CurrentValue;
            return layer.CurrentValue;
        }

        private static Fixed64 GetMemberCapacity(SharedShieldPool pool, int memberId, Fixed64 projectedPoolValue)
        {
            if (pool == null) return Fixed64.Zero;
            var member = FindMember(pool, memberId);
            if (member == null || !member.Active) return Fixed64.Zero;
            var capacity = projectedPoolValue;
            if (member.MaxConsumeValue > Fixed64.Zero)
                capacity = DeterministicMath.Min(capacity, DeterministicMath.Max(Fixed64.Zero, member.MaxConsumeValue - member.ConsumedValue));
            if (pool.SharePolicy == ShieldSharePolicy.WeightedMemberShare)
            {
                var totalWeight = Fixed64.Zero;
                if (pool.Members != null)
                    for (var i = 0; i < pool.Members.Count; i++)
                        if (pool.Members[i] != null && pool.Members[i].Active) totalWeight += pool.Members[i].Weight;
                if (totalWeight > Fixed64.Zero) capacity = DeterministicMath.Min(capacity, projectedPoolValue * member.Weight / totalWeight);
            }
            return capacity;
        }

        private static SharedShieldPoolMember FindMember(SharedShieldPool pool, int memberId)
        {
            if (pool?.Members == null) return null;
            for (var i = 0; i < pool.Members.Count; i++)
                if (pool.Members[i] != null && pool.Members[i].MemberId == memberId) return pool.Members[i];
            return null;
        }

        private int BindPool(SharedShieldPool pool, int actorId, int frame, Fixed64 weight, Fixed64 maxConsumeValue)
        {
            if (pool == null || actorId <= 0) return 0;
            if (pool.Members == null) pool.Members = new List<SharedShieldPoolMember>();
            for (var i = 0; i < pool.Members.Count; i++)
            {
                var existing = pool.Members[i];
                if (existing != null && existing.ActorId == actorId && existing.Active) return existing.MemberId;
            }

            var member = new SharedShieldPoolMember
            {
                MemberId = ++pool.NextMemberId,
                ActorId = actorId,
                Weight = weight <= Fixed64.Zero ? Fixed64.One : weight,
                MaxConsumeValue = maxConsumeValue,
                JoinedFrame = frame,
                LeftFrame = 0,
                Active = true,
            };
            pool.Members.Add(member);
            var container = GetOrCreateContainer(actorId);
            var layer = new ShieldLayer
            {
                InstanceId = ++container.NextInstanceId,
                ShieldId = pool.ShieldId,
                SourceActorId = pool.SourceActorId,
                OwnerActorId = pool.OwnerActorId,
                TargetActorId = actorId,
                SourceContextId = pool.SourceContextId,
                RootContextId = pool.RootContextId,
                OwnerContextId = pool.OwnerContextId,
                SharedPoolId = pool.PoolId,
                SharedPoolMemberId = member.MemberId,
                UsesSharedPoolValue = true,
                CurrentValue = pool.CurrentValue,
                MaxValue = pool.MaxValue,
                InitialValue = pool.InitialValue,
                AbsorbRatio = pool.AbsorbRatio,
                Priority = pool.Priority,
                DamageTypeMask = pool.DamageTypeMask,
                StartFrame = pool.StartFrame,
                ExpireFrame = pool.ExpireFrame,
                RemoveWhenDepleted = pool.RemoveWhenDepleted,
                StackingPolicy = pool.StackingPolicy,
                ConsumePolicy = pool.ConsumePolicy,
                SharePolicy = pool.SharePolicy,
                TransferPolicy = pool.TransferPolicy,
            };
            container.Layers.Add(layer);
            TrackPoolActor(actorId, pool.PoolId);
            Recalculate(container);
            pool.Version++;
            return member.MemberId;
        }

        private static bool CanBindPool(SharedShieldPool pool, int actorId)
        {
            if (pool?.Members == null || actorId <= 0) return false;
            for (var i = 0; i < pool.Members.Count; i++)
            {
                var member = pool.Members[i];
                if (member != null && member.ActorId == actorId && member.Active) return false;
            }
            return true;
        }

        private void TrackPoolActor(int actorId, int poolId)
        {
            var actorPoolIds = ActorPoolIds;
            if (!actorPoolIds.TryGetValue(actorId, out var ids) || ids == null)
            {
                ids = new List<int>(2);
                actorPoolIds[actorId] = ids;
            }
            if (!ids.Contains(poolId)) ids.Add(poolId);
        }

        private void RemovePoolBindings(int actorId, int poolId)
        {
            if (_containers.TryGetValue(actorId, out var container) && container?.Layers != null)
            {
                container.Layers.RemoveAll(x => x != null && x.UsesSharedPoolValue && x.SharedPoolId == poolId);
                Recalculate(container);
            }
            if (_actorPoolIds != null && _actorPoolIds.TryGetValue(actorId, out var ids) && ids != null)
            {
                ids.Remove(poolId);
                if (ids.Count == 0) _actorPoolIds.Remove(actorId);
            }
        }

        private void RefreshPoolBindings(SharedShieldPool pool)
        {
            if (pool == null || pool.Members == null) return;
            for (var i = 0; i < pool.Members.Count; i++)
            {
                var member = pool.Members[i];
                if (member == null || !member.Active || !_containers.TryGetValue(member.ActorId, out var container)) continue;
                if (container?.Layers == null) continue;
                for (var j = 0; j < container.Layers.Count; j++)
                {
                    var layer = container.Layers[j];
                    if (layer != null && layer.UsesSharedPoolValue && layer.SharedPoolId == pool.PoolId && layer.SharedPoolMemberId == member.MemberId)
                    {
                        layer.CurrentValue = pool.CurrentValue;
                    }
                }
                Recalculate(container);
            }
        }

        private void RemoveDepletedSharedPools(int currentFrame = int.MinValue)
        {
            if (_sharedPools == null || _sharedPools.Count == 0) return;
            if (_poolCleanupIds == null) _poolCleanupIds = new List<int>(4);
            _poolCleanupIds.Clear();
            foreach (var pair in _sharedPools)
            {
                var pool = pair.Value;
                if (pool == null) continue;
                var effectiveFrame = currentFrame != int.MinValue ? currentFrame : CurrentFrame();
                var expired = pool.ExpireFrame > 0 && effectiveFrame != int.MinValue && effectiveFrame >= pool.ExpireFrame;
                if (pool.RemoveWhenDepleted && pool.CurrentValue <= Fixed64.Zero || expired) _poolCleanupIds.Add(pair.Key);
            }
            for (var i = 0; i < _poolCleanupIds.Count; i++) RemoveSharedPool(_poolCleanupIds[i]);
        }

        private static ShieldLayer CloneLayer(ShieldLayer source, int targetActorId, Fixed64 value,
            int transferredFromActorId, int transferredToActorId, int frame)
        {
            return new ShieldLayer
            {
                ShieldId = source.ShieldId,
                SourceActorId = source.SourceActorId,
                OwnerActorId = source.OwnerActorId,
                TargetActorId = targetActorId,
                SourceContextId = source.SourceContextId,
                RootContextId = source.RootContextId,
                OwnerContextId = source.OwnerContextId,
                CurrentValue = value,
                MaxValue = value,
                InitialValue = value,
                AbsorbRatio = source.AbsorbRatio,
                Priority = source.Priority,
                DamageTypeMask = source.DamageTypeMask,
                StartFrame = frame,
                ExpireFrame = source.ExpireFrame,
                RemoveWhenDepleted = source.RemoveWhenDepleted,
                StackingPolicy = ShieldStackingPolicy.Independent,
                ConsumePolicy = source.ConsumePolicy,
                SharePolicy = ShieldSharePolicy.None,
                TransferPolicy = source.TransferPolicy,
                TransferredFromActorId = transferredFromActorId,
                TransferredToActorId = transferredToActorId,
                TransferredAtFrame = frame,
                TransferRatio = value,
            };
        }

        private static bool IsMemberActive(SharedShieldPool pool, int memberId, int frame)
        {
            if (pool?.Members == null) return false;
            for (var i = 0; i < pool.Members.Count; i++)
            {
                var member = pool.Members[i];
                if (member == null || member.MemberId != memberId) continue;
                if (!member.Active) return false;
                if (frame != int.MinValue && frame < member.JoinedFrame) return false;
                return member.LeftFrame <= 0 || frame == int.MinValue || frame < member.LeftFrame;
            }
            return false;
        }

        private static void NormalizeLayer(int targetActorId, ShieldLayer layer)
        {
            layer.TargetActorId = targetActorId;
            layer.CurrentValue = DeterministicMath.Max(Fixed64.Zero, layer.CurrentValue > Fixed64.Zero ? layer.CurrentValue : layer.InitialValue);
            layer.MaxValue = DeterministicMath.Max(layer.MaxValue, layer.CurrentValue);
            layer.InitialValue = DeterministicMath.Max(layer.InitialValue, layer.CurrentValue);
            layer.AbsorbRatio = layer.AbsorbRatio <= Fixed64.Zero ? Fixed64.One : DeterministicMath.Min(Fixed64.One, layer.AbsorbRatio);
            layer.RemoveWhenDepleted = true;
        }

        private static ShieldLayer FindStackTarget(ShieldContainer container, ShieldLayer incoming)
        {
            if (container == null || incoming == null || container.Layers == null) return null;
            if (incoming.StackingPolicy != ShieldStackingPolicy.MergeSameShieldAndSource && incoming.StackingPolicy != ShieldStackingPolicy.RefreshSameShieldAndSource) return null;

            for (var i = 0; i < container.Layers.Count; i++)
            {
                var layer = container.Layers[i];
                if (layer == null) continue;
                if (layer.ShieldId == incoming.ShieldId && layer.SourceActorId == incoming.SourceActorId) return layer;
            }

            return null;
        }

        private static void ApplyStacking(ShieldLayer existing, ShieldLayer incoming)
        {
            if (existing == null || incoming == null) return;

            if (incoming.StackingPolicy == ShieldStackingPolicy.RefreshSameShieldAndSource)
            {
                existing.CurrentValue = incoming.CurrentValue;
                existing.MaxValue = incoming.MaxValue;
                existing.InitialValue = incoming.InitialValue;
            }
            else
            {
                existing.CurrentValue += incoming.CurrentValue;
                existing.MaxValue += incoming.MaxValue;
                existing.InitialValue += incoming.InitialValue;
            }

            existing.AbsorbRatio = incoming.AbsorbRatio;
            existing.Priority = incoming.Priority;
            existing.DamageTypeMask = incoming.DamageTypeMask;
            existing.StartFrame = incoming.StartFrame;
            existing.ExpireFrame = incoming.ExpireFrame;
            existing.ConsumePolicy = incoming.ConsumePolicy;
        }

        private static ShieldLayer FindLayer(ShieldContainer container, int instanceId)
        {
            if (container == null || container.Layers == null) return null;
            for (var i = 0; i < container.Layers.Count; i++)
            {
                var layer = container.Layers[i];
                if (layer != null && layer.InstanceId == instanceId) return layer;
            }
            return null;
        }

        private bool CanAbsorb(ShieldLayer layer, DamageType damageType, int currentFrame)
        {
            if (layer == null) return false;
            if (GetEffectiveCurrentValue(layer) <= Fixed64.Zero) return false;
            if (currentFrame != int.MinValue && layer.StartFrame > 0 && currentFrame < layer.StartFrame) return false;
            if (currentFrame != int.MinValue && layer.ExpireFrame > 0 && currentFrame >= layer.ExpireFrame) return false;
            if (layer.UsesSharedPoolValue && (!TryGetSharedPoolState(layer.SharedPoolId, out var pool) ||
                !IsMemberActive(pool, layer.SharedPoolMemberId, currentFrame))) return false;
            if (layer.DamageTypeMask == 0) return true;
            return (layer.DamageTypeMask & (int)damageType) != 0;
        }

        private static bool IsExpired(ShieldLayer layer, int currentFrame)
        {
            return layer != null && layer.ExpireFrame > 0 && currentFrame >= layer.ExpireFrame;
        }

        private static void SortLayers(List<ShieldLayer> layers)
        {
            layers.Sort((a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;

                var priority = b.Priority.CompareTo(a.Priority);
                if (priority != 0) return priority;

                return ResolveConsumeOrder(a, b);
            });
        }

        private static int ResolveConsumeOrder(ShieldLayer a, ShieldLayer b)
        {
            var policy = a.ConsumePolicy;
            switch (policy)
            {
                case ShieldConsumePolicy.PriorityThenNewest:
                case ShieldConsumePolicy.NewestFirst:
                    return b.InstanceId.CompareTo(a.InstanceId);
                case ShieldConsumePolicy.OldestFirst:
                case ShieldConsumePolicy.PriorityThenOldest:
                default:
                    return a.InstanceId.CompareTo(b.InstanceId);
            }
        }

        private void RemoveDepleted(ShieldContainer container)
        {
            if (container == null || container.Layers == null) return;
            container.Layers.RemoveAll(x => x == null ||
                (x.RemoveWhenDepleted && GetEffectiveCurrentValue(x) <= Fixed64.Zero));
        }

        private void Recalculate(ShieldContainer container)
        {
            if (container == null) return;
            // Maintain canonical order when state changes so PreviewAbsorb can remain read-only
            // and allocation-free with respect to candidate ordering.
            if (container.Layers != null && container.Layers.Count > 1)
                SortLayers(container.Layers);
            var total = Fixed64.Zero;
            if (container.Layers != null)
            {
                for (var i = 0; i < container.Layers.Count; i++)
                {
                    var layer = container.Layers[i];
                    if (layer != null) total += GetEffectiveCurrentValue(layer);
                }
            }

            container.TotalRemaining = total;
            container.Dirty = true;
        }

        public void Dispose()
        {
            _containers.Clear();
            _sharedPools?.Clear();
            _actorPoolIds?.Clear();
            _nextPoolId = 0;
            _poolCleanupIds?.Clear();
            _targetScratch?.Clear();
            _targetScratchSet?.Clear();
        }
    }

    public sealed class ShieldAbsorbPlan
    {
        private List<ShieldAbsorbEntry> _entries;

        internal ShieldAbsorbPlan(int targetActorId)
        {
            TargetActorId = targetActorId;
        }

        public int TargetActorId { get; }
        public Fixed64 Absorbed { get; private set; }
        internal IReadOnlyList<ShieldAbsorbEntry> Entries => _entries ?? (IReadOnlyList<ShieldAbsorbEntry>)Array.Empty<ShieldAbsorbEntry>();

        internal void AddLayer(int instanceId, Fixed64 oldValue, Fixed64 consumedValue)
        {
            if (_entries == null) _entries = new List<ShieldAbsorbEntry>(1);
            _entries.Add(ShieldAbsorbEntry.Layer(instanceId, oldValue, consumedValue));
            Absorbed += consumedValue;
        }

        internal void AddPool(int instanceId, int poolId, int memberId, int expectedVersion,
            Fixed64 oldPoolValue, Fixed64 consumedValue)
        {
            if (_entries == null) _entries = new List<ShieldAbsorbEntry>(1);
            _entries.Add(ShieldAbsorbEntry.Pool(instanceId, poolId, memberId, expectedVersion, oldPoolValue, consumedValue));
            Absorbed += consumedValue;
        }

        internal Fixed64 GetProjectedPoolValue(int poolId, Fixed64 defaultValue)
        {
            var value = defaultValue;
            if (_entries == null) return value;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].PoolId == poolId) value -= _entries[i].ConsumedValue;
            }
            return value;
        }

        internal bool IsCommitted { get; private set; }
        internal void MarkCommitted() => IsCommitted = true;
        internal void MarkRolledBack() => IsCommitted = false;
    }

    internal readonly struct ShieldAbsorbEntry
    {
        private ShieldAbsorbEntry(int instanceId, int poolId, int poolMemberId, int expectedPoolVersion,
            Fixed64 oldValue, Fixed64 oldPoolValue, Fixed64 consumedValue)
        {
            InstanceId = instanceId;
            PoolId = poolId;
            PoolMemberId = poolMemberId;
            ExpectedPoolVersion = expectedPoolVersion;
            OldValue = oldValue;
            OldPoolValue = oldPoolValue;
            ConsumedValue = consumedValue;
        }

        public static ShieldAbsorbEntry Layer(int instanceId, Fixed64 oldValue, Fixed64 consumedValue)
            => new ShieldAbsorbEntry(instanceId, 0, 0, 0, oldValue, Fixed64.Zero, consumedValue);

        public static ShieldAbsorbEntry Pool(int instanceId, int poolId, int memberId, int version,
            Fixed64 oldPoolValue, Fixed64 consumedValue)
            => new ShieldAbsorbEntry(instanceId, poolId, memberId, version, Fixed64.Zero, oldPoolValue, consumedValue);

        public int InstanceId { get; }
        public int PoolId { get; }
        public int PoolMemberId { get; }
        public int ExpectedPoolVersion { get; }
        public Fixed64 OldValue { get; }
        public Fixed64 OldPoolValue { get; }
        public Fixed64 ConsumedValue { get; }
    }
}
