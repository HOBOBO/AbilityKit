using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaContinuousOwnerBoundTriggerLifecycleBinder : IContinuousLifecycleBinder, IMobaOwnerBoundTriggerGate, IMobaOwnerBoundTriggerExecutionSourceProvider, IDisposable
    {
        private readonly MobaTriggerExecutionGateway _triggers;
        private readonly MobaOwnerBoundTriggerGateService _gates;
        private readonly Dictionary<long, Binding> _bindingsByOwnerKey = new Dictionary<long, Binding>();
        private readonly Dictionary<IContinuous, long> _ownerKeyByContinuous = new Dictionary<IContinuous, long>();

        public MobaContinuousOwnerBoundTriggerLifecycleBinder(MobaTriggerExecutionGateway triggers, MobaOwnerBoundTriggerGateService gates)
        {
            _triggers = triggers;
            _gates = gates;
            _gates?.RegisterGate(this);
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            if (continuous == null || continuous.Config == null) return;
            if (!(continuous.Config is IMobaContinuousOwnerBoundTriggerConfig triggerConfig)) return;

            var triggerIds = triggerConfig.TriggerIds;
            if (triggerIds == null || triggerIds.Count == 0) return;
            if (!(continuous is IMobaContinuousExecutionContextProvider provider))
            {
                Log.Warning($"[MobaContinuousOwnerBoundTriggerLifecycle] continuous has owner-bound triggers but no execution context provider. type={continuous.GetType().FullName}");
                return;
            }

            if (!provider.TryGetCombatExecutionContext(out var context) || !context.HasExecutionSource)
            {
                Log.Warning($"[MobaContinuousOwnerBoundTriggerLifecycle] continuous has owner-bound triggers but no execution source. type={continuous.GetType().FullName}");
                return;
            }

            var ownerKey = CreateSubscriptionOwnerKey(continuous, context);
            if (ownerKey == 0) return;

            StopExisting(continuous, "continuous.owner_bound.replace");

            var sourceContextId = context.ParentContextId;
            var rootContextId = context.RootContextId != 0 ? context.RootContextId : sourceContextId;
            var ownerContextId = context.OwnerContextId != 0 ? context.OwnerContextId : sourceContextId;
            var source = new MobaOwnerBoundTriggerExecutionSource(
                context.SourceActorId,
                context.TargetActorId > 0 ? context.TargetActorId : context.SourceActorId,
                sourceContextId,
                rootContextId,
                ownerContextId,
                context.ConfigId);

            if (!source.HasExecutionSource) return;

            var binding = new Binding(continuous, CopyTriggerIds(triggerIds), source);
            _bindingsByOwnerKey[ownerKey] = binding;
            _ownerKeyByContinuous[continuous] = ownerKey;
            _triggers?.ApplyOwnerBoundTriggers(binding.TriggerIds, ownerKey, "continuous.owner_bound.activate");
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            StopExisting(continuous, "continuous.owner_bound.end");
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            StopExisting(continuous, "continuous.owner_bound.unregister");
        }

        public bool IsMatch(long ownerKey, int triggerId)
        {
            return TryGetBinding(ownerKey, triggerId, out _);
        }

        public bool CanExecute(long ownerKey, int triggerId)
        {
            if (!TryGetBinding(ownerKey, triggerId, out var binding)) return true;
            var continuous = binding.Continuous;
            return continuous != null && continuous.IsActive && !continuous.IsPaused && !continuous.IsTerminated;
        }

        public void Complete(long ownerKey, int triggerId)
        {
        }

        public bool TryGetExecutionSource(long ownerKey, int triggerId, out MobaOwnerBoundTriggerExecutionSource source)
        {
            source = default;
            if (!TryGetBinding(ownerKey, triggerId, out var binding)) return false;
            source = binding.Source;
            return source.HasExecutionSource;
        }

        public void Dispose()
        {
            var keys = new List<long>(_bindingsByOwnerKey.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                _triggers?.StopOwnerBoundTriggers(keys[i], "continuous.owner_bound.dispose");
            }

            _bindingsByOwnerKey.Clear();
            _ownerKeyByContinuous.Clear();
            _gates?.UnregisterGate(this);
        }

        private bool TryGetBinding(long ownerKey, int triggerId, out Binding binding)
        {
            binding = null;
            if (ownerKey == 0 || triggerId <= 0) return false;
            if (!_bindingsByOwnerKey.TryGetValue(ownerKey, out binding) || binding == null) return false;
            return ContainsTriggerId(binding.TriggerIds, triggerId);
        }

        private void StopExisting(IContinuous continuous, string source)
        {
            if (continuous == null) return;
            if (!_ownerKeyByContinuous.TryGetValue(continuous, out var ownerKey) || ownerKey == 0) return;

            _ownerKeyByContinuous.Remove(continuous);
            _bindingsByOwnerKey.Remove(ownerKey);
            _triggers?.StopOwnerBoundTriggers(ownerKey, source);
        }

        private static int[] CopyTriggerIds(IReadOnlyList<int> triggerIds)
        {
            if (triggerIds == null || triggerIds.Count == 0) return Array.Empty<int>();

            var copy = new int[triggerIds.Count];
            for (int i = 0; i < triggerIds.Count; i++) copy[i] = triggerIds[i];
            return copy;
        }

        private static bool ContainsTriggerId(IReadOnlyList<int> triggerIds, int triggerId)
        {
            if (triggerIds == null || triggerIds.Count == 0) return false;
            for (int i = 0; i < triggerIds.Count; i++)
            {
                if (triggerIds[i] == triggerId) return true;
            }

            return false;
        }

        private static long CreateSubscriptionOwnerKey(IContinuous continuous, in MobaCombatExecutionContext context)
        {
            unchecked
            {
                const long offset = 1469598103934665603L;
                const long prime = 1099511628211L;
                var hash = offset;
                hash = (hash ^ context.ParentContextId) * prime;
                hash = (hash ^ context.RootContextId) * prime;
                hash = (hash ^ context.OwnerContextId) * prime;
                hash = (hash ^ context.SourceActorId) * prime;
                hash = (hash ^ context.TargetActorId) * prime;
                hash = (hash ^ context.ConfigId) * prime;

                var id = continuous?.Config?.Id;
                if (!string.IsNullOrEmpty(id))
                {
                    for (int i = 0; i < id.Length; i++) hash = (hash ^ id[i]) * prime;
                }

                if (hash == 0) hash = -1;
                return hash > 0 ? -hash : hash;
            }
        }

        private sealed class Binding
        {
            public Binding(IContinuous continuous, IReadOnlyList<int> triggerIds, MobaOwnerBoundTriggerExecutionSource source)
            {
                Continuous = continuous;
                TriggerIds = triggerIds ?? Array.Empty<int>();
                Source = source;
            }

            public IContinuous Continuous { get; }
            public IReadOnlyList<int> TriggerIds { get; }
            public MobaOwnerBoundTriggerExecutionSource Source { get; }
        }
    }
}
