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
            state.Current = DeterministicMath.Max(Fixed64.Zero, value);
            MobaResourceAttributeContextProjector.Refresh(actor);
        }
    }
}
