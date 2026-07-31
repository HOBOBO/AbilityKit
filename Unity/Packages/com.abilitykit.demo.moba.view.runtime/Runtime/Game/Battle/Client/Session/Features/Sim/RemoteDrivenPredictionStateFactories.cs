using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Game.Flow
{
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

            var calculator = new MobaAuthoritativeStateHashCalculator();
            return _ =>
            {
                var hash = calculator.Compute(phase.InGame, registry);
                if (ShouldForceMismatch(shouldForceMismatch))
                {
                    hash ^= 1u;
                }

                return new WorldStateHash(hash);
            };
        }

        private static bool ShouldForceMismatch(Func<bool> shouldForceMismatch)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return shouldForceMismatch != null && shouldForceMismatch();
#else
            return false;
#endif
        }

    }
}
