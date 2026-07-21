using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Flow
{
    internal static class RemoteDrivenRollbackRegistryFactory
    {
        public static RollbackRegistry Create(IWorld world)
        {
            var registry = new RollbackRegistry();
            if (world?.Services == null) return registry;

            if (world.Services.TryResolve<IFrameTime>(out var frameTime) && frameTime is FrameTime mutableFrameTime)
            {
                registry.Register(new FrameTimeRollbackStateProvider(mutableFrameTime));
            }

            if (world.Services.TryResolve<MobaActorRegistry>(out var actorRegistry) && actorRegistry != null)
            {
                registry.Register(new MobaActorTransformRollbackProvider(actorRegistry));
            }

            if (world.Services.TryResolve<PassiveSkillTriggerEventRollbackLog>(out var passiveLog) && passiveLog != null)
            {
                registry.Register(passiveLog);
            }

            if (world.Services.TryResolve<RollbackWorldRandom>(out var random) && random != null)
            {
                registry.Register(random);
            }

            return registry;
        }
    }

    internal static class RemoteDrivenStateHashFactory
    {
        public static Func<FrameIndex, WorldStateHash> Create(IWorld world, Func<bool> shouldForceMismatch)
        {
            if (world?.Services == null) return null;

            if (!world.Services.TryResolve<MobaLogicWorldRunGateService>(out var phase) || phase == null)
            {
                return null;
            }

            if (!world.Services.TryResolve<MobaActorRegistry>(out var registry) || registry == null)
            {
                return null;
            }

            return _ => new WorldStateHash(ComputeStateHash(phase, registry, shouldForceMismatch));
        }

        private static uint ComputeStateHash(
            MobaLogicWorldRunGateService phase,
            MobaActorRegistry registry,
            Func<bool> shouldForceMismatch)
        {
            var entries = new List<(int actorId, float x, float y, float z)>(16);
            foreach (var pair in registry.Entries)
            {
                var actorId = pair.Key;
                var entity = pair.Value;
                if (entity == null) continue;
                if (!entity.hasTransform) continue;

                var position = entity.transform.Value.Position;
                entries.Add((actorId, position.X, position.Y, position.Z));
            }

            entries.Sort((a, b) => a.actorId.CompareTo(b.actorId));

            uint hash = 2166136261u;
            AddByte(ref hash, phase != null && phase.InGame ? (byte)1 : (byte)0);
            AddInt(ref hash, entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var item = entries[i];
                AddInt(ref hash, item.actorId);
                AddFloat(ref hash, item.x);
                AddFloat(ref hash, item.y);
                AddFloat(ref hash, item.z);
            }

            if (ShouldForceMismatch(shouldForceMismatch))
            {
                hash ^= 1u;
            }

            return hash;
        }

        private static bool ShouldForceMismatch(Func<bool> shouldForceMismatch)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return shouldForceMismatch != null && shouldForceMismatch();
#else
            return false;
#endif
        }

        private static void AddByte(ref uint hash, byte value)
        {
            hash ^= value;
            hash *= 16777619u;
        }

        private static void AddUInt(ref uint hash, uint value)
        {
            AddByte(ref hash, (byte)(value & 0xFF));
            AddByte(ref hash, (byte)((value >> 8) & 0xFF));
            AddByte(ref hash, (byte)((value >> 16) & 0xFF));
            AddByte(ref hash, (byte)((value >> 24) & 0xFF));
        }

        private static void AddInt(ref uint hash, int value)
        {
            unchecked
            {
                AddUInt(ref hash, (uint)value);
            }
        }

        private static void AddFloat(ref uint hash, float value)
        {
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value));
        }
    }
}
