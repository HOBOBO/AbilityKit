using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaRuntimeLifecycleEventKind
    {
        Activated,
        Ended,
        Cleared,
        Failed
    }

    public readonly struct MobaRuntimeLifecycleEvent
    {
        public MobaRuntimeLifecycleEvent(MobaRuntimeLifecycleEventKind kind, object runtime, in MobaContextSourceView source, string reason)
        {
            Kind = kind;
            Runtime = runtime;
            Source = source;
            Reason = reason;
        }

        public MobaRuntimeLifecycleEventKind Kind { get; }
        public object Runtime { get; }
        public MobaContextSourceView Source { get; }
        public string Reason { get; }
    }

    public interface IMobaRuntimeLifecycleHook
    {
        void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent);
    }

    public sealed class MobaRuntimeLifecycleHookService
    {
        private readonly List<IMobaRuntimeLifecycleHook> _hooks = new List<IMobaRuntimeLifecycleHook>(4);

        public void Register(IMobaRuntimeLifecycleHook hook)
        {
            if (hook == null || _hooks.Contains(hook)) return;
            _hooks.Add(hook);
        }

        public void Notify(in MobaRuntimeLifecycleEvent lifecycleEvent)
        {
            for (var i = 0; i < _hooks.Count; i++)
            {
                _hooks[i]?.OnRuntimeLifecycle(in lifecycleEvent);
            }
        }
    }

    public sealed class MobaSkillRuntimeLifecycleBridgeHook : IMobaSkillRuntimeLifecycleHook
    {
        private readonly MobaRuntimeLifecycleHookService _runtimeHooks;

        public MobaSkillRuntimeLifecycleBridgeHook(MobaRuntimeLifecycleHookService runtimeHooks)
        {
            _runtimeHooks = runtimeHooks;
        }

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            if (_runtimeHooks == null || lifecycleEvent.Runtime == null) return;
            if (!TryMapKind(lifecycleEvent.Kind, lifecycleEvent.Forced, out var kind)) return;

            lifecycleEvent.Runtime.TryGetContextSource(out var source);
            var reason = lifecycleEvent.Reason.ToString();
            var runtimeEvent = new MobaRuntimeLifecycleEvent(kind, lifecycleEvent.Runtime, in source, reason);
            _runtimeHooks.Notify(in runtimeEvent);
        }

        private static bool TryMapKind(MobaSkillRuntimeLifecycleEventKind skillKind, bool forced, out MobaRuntimeLifecycleEventKind kind)
        {
            switch (skillKind)
            {
                case MobaSkillRuntimeLifecycleEventKind.Created:
                    kind = MobaRuntimeLifecycleEventKind.Activated;
                    return true;
                case MobaSkillRuntimeLifecycleEventKind.Finalized:
                    kind = forced ? MobaRuntimeLifecycleEventKind.Failed : MobaRuntimeLifecycleEventKind.Ended;
                    return true;
                case MobaSkillRuntimeLifecycleEventKind.Cleared:
                    kind = MobaRuntimeLifecycleEventKind.Cleared;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }

    public static class MobaRuntimeLifecycleHookFactory
    {
        public static MobaSkillRuntimeLifecycleBridgeHook CreateSkillRuntimeBridge(MobaRuntimeLifecycleHookService runtimeHooks)
        {
            return runtimeHooks != null ? new MobaSkillRuntimeLifecycleBridgeHook(runtimeHooks) : null;
        }

        public static MobaRuntimeLifecycleHookService CreateDefault(IMobaRuntimeLifecycleHook optionalHook = null)
        {
            var hooks = new MobaRuntimeLifecycleHookService();
            if (optionalHook != null)
            {
                hooks.Register(optionalHook);
            }
 
            return hooks;
        }
    }
}
