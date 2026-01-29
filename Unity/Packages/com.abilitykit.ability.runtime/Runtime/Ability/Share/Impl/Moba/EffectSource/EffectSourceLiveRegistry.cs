#if UNITY_EDITOR

using System;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    public static class EffectSourceLiveRegistry
    {
        static WeakReference _current;

        public static event Action Changed;

        public static void Register(EffectSourceRegistry registry)
        {
            if (registry == null) return;
            _current = new WeakReference(registry);
            Changed?.Invoke();
        }

        public static void Unregister(EffectSourceRegistry registry)
        {
            if (_current == null) return;
            if (registry == null) return;

            var target = _current.Target;
            if (target == null || ReferenceEquals(target, registry))
            {
                _current = null;
                Changed?.Invoke();
            }
        }

        public static EffectSourceRegistry GetCurrent()
        {
            return _current?.Target as EffectSourceRegistry;
        }
    }
}

#endif
