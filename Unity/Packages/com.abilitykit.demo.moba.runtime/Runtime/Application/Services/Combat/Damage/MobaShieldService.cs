using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaShieldService))]
    public sealed class MobaShieldService : IService
    {
        private readonly Dictionary<int, ShieldContainer> _containers = new Dictionary<int, ShieldContainer>();
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

        public float Absorb(AttackInfo attack, float incomingDamage)
        {
            if (attack == null) return 0f;
            if (incomingDamage <= 0f) return 0f;
            if (!_containers.TryGetValue(attack.TargetActorId, out var container) || container == null) return 0f;
            if (container.Layers == null || container.Layers.Count == 0) return 0f;

            SortLayers(container.Layers);

            var remainingDamage = incomingDamage;
            var absorbed = 0f;

            for (var i = 0; i < container.Layers.Count && remainingDamage > 0f; i++)
            {
                var layer = container.Layers[i];
                if (!CanAbsorb(layer, attack.DamageType)) continue;

                var ratio = layer.AbsorbRatio <= 0f ? 1f : Math.Min(1f, layer.AbsorbRatio);
                var wanted = remainingDamage * ratio;
                var take = Math.Min(layer.CurrentValue, wanted);
                if (take <= 0f) continue;

                layer.CurrentValue -= take;
                remainingDamage -= take;
                absorbed += take;
            }

            RemoveDepleted(container);
            Recalculate(container);
            return absorbed;
        }

        public float GetTotalRemaining(int targetActorId)
        {
            return _containers.TryGetValue(targetActorId, out var container) && container != null ? container.TotalRemaining : 0f;
        }

        public bool TryGetContainer(int targetActorId, out ShieldContainer container)
        {
            return _containers.TryGetValue(targetActorId, out container) && container != null;
        }

        public bool RemoveActor(int actorId)
        {
            return actorId > 0 && _containers.Remove(actorId);
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
                container.Layers.RemoveAll(x => x == null || IsExpired(x, currentFrame) || (x.RemoveWhenDepleted && x.CurrentValue <= 0f));
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
                TotalRemaining = 0f,
                Dirty = false,
            };
            _containers[targetActorId] = container;
            return container;
        }

        private static void NormalizeLayer(int targetActorId, ShieldLayer layer)
        {
            layer.TargetActorId = targetActorId;
            layer.CurrentValue = Math.Max(0f, layer.CurrentValue > 0f ? layer.CurrentValue : layer.InitialValue);
            layer.MaxValue = Math.Max(layer.MaxValue, layer.CurrentValue);
            layer.InitialValue = Math.Max(layer.InitialValue, layer.CurrentValue);
            layer.AbsorbRatio = layer.AbsorbRatio <= 0f ? 1f : Math.Min(1f, layer.AbsorbRatio);
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

        private static bool CanAbsorb(ShieldLayer layer, DamageType damageType)
        {
            if (layer == null) return false;
            if (layer.CurrentValue <= 0f) return false;
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
            container.Layers.RemoveAll(x => x == null || (x.RemoveWhenDepleted && x.CurrentValue <= 0f));
        }

        private static void Recalculate(ShieldContainer container)
        {
            if (container == null) return;
            var total = 0f;
            if (container.Layers != null)
            {
                for (var i = 0; i < container.Layers.Count; i++)
                {
                    var layer = container.Layers[i];
                    if (layer != null && layer.CurrentValue > 0f) total += layer.CurrentValue;
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
}
