using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaShieldService))]
    public sealed class MobaShieldService : IService
    {
        private Dictionary<int, ShieldContainer> _containers = new Dictionary<int, ShieldContainer>();
        private readonly List<int> _cleanupActorIds = new List<int>(32);

        public int AddShield(int targetActorId, ShieldLayer layer)
        {
            if (targetActorId <= 0) return 0;
            if (layer == null) return 0;

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

        public Fixed64 Absorb(AttackInfo attack, Fixed64 incomingDamage)
        {
            var plan = PreviewAbsorb(attack, incomingDamage);
            if (!CommitAbsorb(plan)) return Fixed64.Zero;
            FinalizeAbsorb(plan);
            return plan.Absorbed;
        }

        public ShieldAbsorbPlan PreviewAbsorb(AttackInfo attack, Fixed64 incomingDamage)
        {
            var plan = new ShieldAbsorbPlan(attack != null ? attack.TargetActorId : 0);
            if (attack == null || incomingDamage <= Fixed64.Zero) return plan;
            if (!_containers.TryGetValue(attack.TargetActorId, out var container) || container == null) return plan;
            if (container.Layers == null || container.Layers.Count == 0) return plan;

            var ordered = new List<ShieldLayer>(container.Layers);
            SortLayers(ordered);
            var remainingDamage = incomingDamage;
            for (var i = 0; i < ordered.Count && remainingDamage > Fixed64.Zero; i++)
            {
                var layer = ordered[i];
                if (!CanAbsorb(layer, attack.DamageType)) continue;

                var ratio = layer.AbsorbRatio <= Fixed64.Zero ? Fixed64.One : DeterministicMath.Min(Fixed64.One, layer.AbsorbRatio);
                var take = DeterministicMath.Min(layer.CurrentValue, remainingDamage * ratio);
                if (take <= Fixed64.Zero) continue;

                plan.Add(layer.InstanceId, layer.CurrentValue, take);
                remainingDamage -= take;
            }
            return plan;
        }

        public bool CommitAbsorb(ShieldAbsorbPlan plan)
        {
            if (plan == null || plan.Absorbed <= Fixed64.Zero) return plan != null;
            if (!_containers.TryGetValue(plan.TargetActorId, out var container) || container == null) return false;
            if (container.Layers == null) return false;

            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                var layer = FindLayer(container, entry.InstanceId);
                if (layer == null || layer.CurrentValue != entry.OldValue) return false;
            }

            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                FindLayer(container, entry.InstanceId).CurrentValue = entry.OldValue - entry.ConsumedValue;
            }
            Recalculate(container);
            return true;
        }

        public void RollbackAbsorb(ShieldAbsorbPlan plan)
        {
            if (plan == null || plan.Absorbed <= Fixed64.Zero) return;
            if (!_containers.TryGetValue(plan.TargetActorId, out var container) || container == null) return;
            if (container.Layers == null) return;

            for (var i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];
                var layer = FindLayer(container, entry.InstanceId);
                if (layer != null) layer.CurrentValue = entry.OldValue;
            }
            Recalculate(container);
        }

        public void FinalizeAbsorb(ShieldAbsorbPlan plan)
        {
            if (plan == null || plan.Absorbed <= Fixed64.Zero) return;
            if (!_containers.TryGetValue(plan.TargetActorId, out var container) || container == null) return;

            RemoveDepleted(container);
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

        public bool TryGetContainer(int targetActorId, out ShieldContainer container)
        {
            return _containers.TryGetValue(targetActorId, out container) && container != null;
        }

        public bool RemoveActor(int actorId)
        {
            return actorId > 0 && _containers.Remove(actorId);
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

        public bool RemoveShield(int targetActorId, int instanceId)
        {
            if (targetActorId <= 0 || instanceId <= 0) return false;
            if (!_containers.TryGetValue(targetActorId, out var container) || container == null) return false;
            if (container.Layers == null) return false;

            var removed = container.Layers.RemoveAll(x => x != null && x.InstanceId == instanceId) > 0;
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

                container.Layers.RemoveAt(i);
                removed++;
                if (!removeAll) break;
            }

            if (removed > 0) Recalculate(container);
            return removed;
        }

        public int CleanupExpired(int currentFrame)
        {
            if (currentFrame <= 0 || _containers.Count == 0) return 0;

            _cleanupActorIds.Clear();
            foreach (var kv in _containers)
            {
                _cleanupActorIds.Add(kv.Key);
            }

            var removed = 0;
            for (var i = 0; i < _cleanupActorIds.Count; i++)
            {
                var actorId = _cleanupActorIds[i];
                if (!_containers.TryGetValue(actorId, out var container) || container == null) continue;
                if (container.Layers == null || container.Layers.Count == 0) continue;

                var before = container.Layers.Count;
                container.Layers.RemoveAll(x => x == null || IsExpired(x, currentFrame) || (x.RemoveWhenDepleted && x.CurrentValue <= Fixed64.Zero));
                var delta = before - container.Layers.Count;
                if (delta <= 0) continue;

                removed += delta;
                Recalculate(container);
            }

            return removed;
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

        private static bool CanAbsorb(ShieldLayer layer, DamageType damageType)
        {
            if (layer == null) return false;
            if (layer.CurrentValue <= Fixed64.Zero) return false;
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

        private static void RemoveDepleted(ShieldContainer container)
        {
            if (container == null || container.Layers == null) return;
            container.Layers.RemoveAll(x => x == null || (x.RemoveWhenDepleted && x.CurrentValue <= Fixed64.Zero));
        }

        private static void Recalculate(ShieldContainer container)
        {
            if (container == null) return;
            var total = Fixed64.Zero;
            if (container.Layers != null)
            {
                for (var i = 0; i < container.Layers.Count; i++)
                {
                    var layer = container.Layers[i];
                    if (layer != null && layer.CurrentValue > Fixed64.Zero) total += layer.CurrentValue;
                }
            }

            container.TotalRemaining = total;
            container.Dirty = true;
        }

        public void Dispose()
        {
            _containers.Clear();
            _cleanupActorIds.Clear();
        }
    }

    public sealed class ShieldAbsorbPlan
    {
        private readonly List<ShieldAbsorbEntry> _entries = new List<ShieldAbsorbEntry>();

        internal ShieldAbsorbPlan(int targetActorId)
        {
            TargetActorId = targetActorId;
        }

        public int TargetActorId { get; }
        public Fixed64 Absorbed { get; private set; }
        internal IReadOnlyList<ShieldAbsorbEntry> Entries => _entries;

        internal void Add(int instanceId, Fixed64 oldValue, Fixed64 consumedValue)
        {
            _entries.Add(new ShieldAbsorbEntry(instanceId, oldValue, consumedValue));
            Absorbed += consumedValue;
        }
    }

    internal readonly struct ShieldAbsorbEntry
    {
        public ShieldAbsorbEntry(int instanceId, Fixed64 oldValue, Fixed64 consumedValue)
        {
            InstanceId = instanceId;
            OldValue = oldValue;
            ConsumedValue = consumedValue;
        }

        public int InstanceId { get; }
        public Fixed64 OldValue { get; }
        public Fixed64 ConsumedValue { get; }
    }
}
