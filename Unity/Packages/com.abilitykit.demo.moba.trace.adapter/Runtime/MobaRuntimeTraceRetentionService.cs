using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaRuntimeTraceRetentionService), WorldLifetime.Scoped)]
    public sealed class MobaRuntimeTraceRetentionService :
        IService
    {
        private sealed class RetentionSlot
        {
            public MobaTraceRetentionHandle Handle;
        }

        private readonly MobaTraceRegistry _trace;
        private readonly ConditionalWeakTable<object, RetentionSlot> _retentions =
            new ConditionalWeakTable<object, RetentionSlot>();
        private readonly HashSet<RetentionSlot> _activeRetentions =
            new HashSet<RetentionSlot>();

        public MobaRuntimeTraceRetentionService(MobaTraceRegistry trace)
        {
            _trace = trace;
        }

        public bool IsRetained(object runtime)
        {
            return runtime != null
                   && _retentions.TryGetValue(runtime, out var slot)
                   && slot.Handle != null
                   && slot.Handle.IsValid;
        }

        public void Retain(object runtime, in MobaContextSourceView source, string reason)
        {
            if (_trace == null || runtime == null) return;
            var slot = _retentions.GetOrCreateValue(runtime);
            if (slot.Handle != null && slot.Handle.IsValid) return;

            if (_trace.TryRetainContextSource(in source, reason, out var handle)
                || _trace.TryRetainPayloadSource(runtime, reason, out handle))
            {
                slot.Handle = handle;
                _activeRetentions.Add(slot);
            }
        }

        public void Release(object runtime)
        {
            if (runtime == null) return;
            if (!_retentions.TryGetValue(runtime, out var slot)) return;

            slot.Handle?.Dispose();
            slot.Handle = null;
            _activeRetentions.Remove(slot);
            _retentions.Remove(runtime);
        }

        public void Dispose()
        {
            foreach (var slot in _activeRetentions)
            {
                slot.Handle?.Dispose();
                slot.Handle = null;
            }

            _activeRetentions.Clear();
        }
    }

    [WorldService(typeof(IMobaRuntimeLifecycleHook), WorldLifetime.Scoped)]
    public sealed class MobaTraceRetentionLifecycleHook : IMobaRuntimeLifecycleHook, IService
    {
        private readonly MobaRuntimeTraceRetentionService _retention;

        public MobaTraceRetentionLifecycleHook(MobaRuntimeTraceRetentionService retention)
        {
            _retention = retention;
        }

        public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent.Runtime == null || _retention == null) return;

            switch (lifecycleEvent.Kind)
            {
                case MobaRuntimeLifecycleEventKind.Activated:
                    var source = lifecycleEvent.Source;
                    _retention.Retain(lifecycleEvent.Runtime, in source, lifecycleEvent.Reason);
                    break;
                case MobaRuntimeLifecycleEventKind.Ended:
                case MobaRuntimeLifecycleEventKind.Cleared:
                case MobaRuntimeLifecycleEventKind.Failed:
                    _retention.Release(lifecycleEvent.Runtime);
                    break;
            }
        }

        public bool IsRetained(object runtime)
        {
            return _retention != null && _retention.IsRetained(runtime);
        }

        public void Dispose()
        {
        }
    }
}
