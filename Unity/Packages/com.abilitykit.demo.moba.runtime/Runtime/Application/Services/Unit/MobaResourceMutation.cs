using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class MobaResourceMutation
    {
        public static bool TryGetState(global::ActorEntity actor, ResourceType type, out ResourceState state)
        {
            state = null;
            return type != ResourceType.None && type != ResourceType.Hp && actor != null &&
                   actor.hasResourceContainer && actor.resourceContainer.Value?.Map != null &&
                   actor.resourceContainer.Value.Map.TryGetValue(type, out state) && state != null;
        }

        public static bool TryConsume(global::ActorEntity actor, ResourceType type, Fixed64 amount,
            out Fixed64 before, out Fixed64 after)
        {
            before = after = Fixed64.Zero;
            if (amount <= Fixed64.Zero || !TryGetState(actor, type, out var state)) return false;
            before = after = state.Current;
            if (state.Current < amount) return false;
            state.Current -= amount;
            try
            {
                MobaResourceAttributeContextProjector.Refresh(actor);
            }
            catch
            {
                state.Current = before;
                throw;
            }
            after = state.Current;
            return true;
        }

        public static void Refund(global::ActorEntity actor, ResourceType type, ResourceState state, Fixed64 amount)
        {
            if (!TryGetState(actor, type, out var current) || !ReferenceEquals(current, state) || amount <= Fixed64.Zero)
                return;
            var next = state.Current + amount;
            state.Current = state.LastMax > Fixed64.Zero ? DeterministicMath.Min(next, state.LastMax) : next;
            MobaResourceAttributeContextProjector.Refresh(actor);
        }

        public static void Set(global::ActorEntity actor, ResourceType type, ResourceState state, Fixed64 value)
        {
            if (!TryGetState(actor, type, out var current) || !ReferenceEquals(current, state))
                throw new System.InvalidOperationException("Resource mutations cannot directly write health or a detached resource.");
            var before = state.Current;
            state.Current = DeterministicMath.Max(Fixed64.Zero, value);
            try { MobaResourceAttributeContextProjector.Refresh(actor); }
            catch { state.Current = before; throw; }
        }

        /// <summary>
        /// 固定量回复只补充不足部分；动态上限失效时不得回退旧 LastMax。
        /// </summary>
        public static void Regenerate(global::ActorEntity actor, ResourceType type, ResourceState state, Fixed64 amount)
        {
            if (amount <= Fixed64.Zero || !TryGetState(actor, type, out var current) || !ReferenceEquals(current, state)) return;
            var max = state.LastMax;
            if (state.MaxAttribute.IsValid)
            {
                if (!actor.hasAttributeGroup || actor.attributeGroup.Group == null) return;
                var value = actor.attributeGroup.Group.GetValue(state.MaxAttribute);
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new System.InvalidOperationException("Resource maximum must be finite.");
                max = MobaResourceFixedConvert.ToFixed(value);
            }
            if (max <= Fixed64.Zero || state.Current >= max) return;
            var missing = max - state.Current;
            Set(actor, type, state, state.Current + DeterministicMath.Min(amount, missing));
        }
    }
}
