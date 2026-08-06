using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public interface IMobaProjectileEmitterRegistry
    {
        void Register(ProjectileEmitterType emitterType, Func<IMobaProjectileLaunchSequence> factory, int priority = 0, bool isDefault = false);
        bool TryRegister(ProjectileEmitterType emitterType, Func<IMobaProjectileLaunchSequence> factory, int priority = 0, bool isDefault = false);
        bool TryCreate(ProjectileEmitterType emitterType, out IMobaProjectileLaunchSequence sequence);
        bool TryCreateDefault(out IMobaProjectileLaunchSequence sequence);
    }

    public sealed class MobaProjectileEmitterRegistry : IMobaProjectileEmitterRegistry
    {
        private readonly Dictionary<ProjectileEmitterType, Entry> _entries = new Dictionary<ProjectileEmitterType, Entry>();
        private ProjectileEmitterType? _defaultEmitterType;

        public MobaProjectileEmitterRegistry()
        {
        }

        public MobaProjectileEmitterRegistry(Assembly assembly)
        {
            RegisterFromAssembly(assembly);
        }

        public static MobaProjectileEmitterRegistry CreateDefault(Assembly assembly = null)
        {
            var registry = new MobaProjectileEmitterRegistry();
            var targetAssembly = assembly ?? typeof(MobaProjectileEmitterRegistry).Assembly;
            if (targetAssembly != typeof(MobaProjectileEmitterRegistry).Assembly)
            {
                registry.RegisterFromAssembly(targetAssembly);
                return registry;
            }

            if (MobaGeneratedProjectileEmitterManifest.Register(registry) > 0)
            {
                return registry;
            }

            if (AppContext.TryGetSwitch(
                    "AbilityKit.Moba.DisableProjectileEmitterReflectionFallback",
                    out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
            {
                throw new InvalidOperationException(
                    "The generated MOBA projectile emitter manifest is empty and reflection fallback is disabled.");
            }

            registry.RegisterFromAssembly(targetAssembly);
            return registry;
        }

        public void Register(ProjectileEmitterType emitterType, Func<IMobaProjectileLaunchSequence> factory, int priority = 0, bool isDefault = false)
        {
            TryRegister(emitterType, factory, priority, isDefault);
        }

        public bool TryRegister(ProjectileEmitterType emitterType, Func<IMobaProjectileLaunchSequence> factory, int priority = 0, bool isDefault = false)
        {
            if (factory == null) return false;

            if (isDefault && _defaultEmitterType.HasValue && _defaultEmitterType.Value != emitterType)
            {
                throw new InvalidOperationException(
                    $"Ambiguous default MOBA projectile emitter types '{_defaultEmitterType.Value}' and '{emitterType}'.");
            }

            var entry = new Entry(emitterType, factory, priority);
            if (!_entries.TryGetValue(emitterType, out var current))
            {
                _entries.Add(emitterType, entry);
                if (isDefault) _defaultEmitterType = emitterType;
                return true;
            }

            if (priority > current.Priority)
            {
                _entries[emitterType] = entry;
                if (isDefault) _defaultEmitterType = emitterType;
                return true;
            }

            if (priority == current.Priority)
            {
                throw new InvalidOperationException(
                    $"Ambiguous MOBA projectile emitter '{emitterType}' at priority '{priority}'.");
            }

            if (isDefault) _defaultEmitterType = emitterType;
            return false;
        }

        internal int Count => _entries.Count;

        public bool TryCreate(ProjectileEmitterType emitterType, out IMobaProjectileLaunchSequence sequence)
        {
            sequence = null;
            if (!_entries.TryGetValue(emitterType, out var entry))
            {
                return false;
            }

            sequence = entry.Factory?.Invoke();
            return sequence != null;
        }

        public bool TryCreateDefault(out IMobaProjectileLaunchSequence sequence)
        {
            sequence = null;
            return _defaultEmitterType.HasValue && TryCreate(_defaultEmitterType.Value, out sequence);
        }

        internal ProjectileEmitterType? DefaultEmitterType => _defaultEmitterType;

        public void RegisterFromAssembly(Assembly assembly)
        {
            if (assembly == null) return;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null) return;

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IMobaProjectileLaunchSequence).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                var attrs = (MobaProjectileEmitterAttribute[])type.GetCustomAttributes(typeof(MobaProjectileEmitterAttribute), false);
                if (attrs == null || attrs.Length == 0) continue;

                for (int j = 0; j < attrs.Length; j++)
                {
                    var attr = attrs[j];
                    if (attr == null) continue;

                    Register(attr.EmitterType, () => CreateSequence(type), attr.Priority, attr.IsDefault);
                }
            }
        }

        private static IMobaProjectileLaunchSequence CreateSequence(Type type)
        {
            try
            {
                return Activator.CreateInstance(type) as IMobaProjectileLaunchSequence;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaProjectileEmitterRegistry] Create projectile emitter sequence failed. type={type?.FullName}");
                return null;
            }
        }

        private readonly struct Entry
        {
            public Entry(ProjectileEmitterType emitterType, Func<IMobaProjectileLaunchSequence> factory, int priority)
            {
                EmitterType = emitterType;
                Factory = factory;
                Priority = priority;
            }

            public ProjectileEmitterType EmitterType { get; }
            public Func<IMobaProjectileLaunchSequence> Factory { get; }
            public int Priority { get; }
        }
    }
}
