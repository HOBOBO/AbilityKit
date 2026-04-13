using AbilityKit.Core.Math;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Demo.Moba.Triggering.SummonSpawning
{
    public static class SpawnSummonRotationResolver
    {
        public static Vec3 ResolveForward(SpawnSummonSpec.RotationMode mode, MobaActorRegistry registry, int casterActorId, int targetActorId, TriggerContext context)
        {
            switch (mode)
            {
                case SpawnSummonSpec.RotationMode.FaceTarget:
                {
                    if (registry != null && casterActorId > 0 && targetActorId > 0 &&
                        registry.TryGet(casterActorId, out var c) && c != null && c.hasTransform &&
                        registry.TryGet(targetActorId, out var t) && t != null && t.hasTransform)
                    {
                        var from = c.transform.Value.Position;
                        var to = t.transform.Value.Position;
                        return new Vec3(to.X - from.X, 0f, to.Z - from.Z);
                    }
                    return default;
                }
                case SpawnSummonSpec.RotationMode.AimDir:
                {
                    if (context != null && context.Event.Payload is SkillCastContext sc)
                    {
                        return sc.AimDir;
                    }
                    return default;
                }
                case SpawnSummonSpec.RotationMode.Caster:
                default:
                {
                    if (registry != null && casterActorId > 0 && registry.TryGet(casterActorId, out var c) && c != null && c.hasTransform)
                    {
                        var rot = c.transform.Value.Rotation;
                        return rot.Rotate(Vec3.Forward);
                    }
                    return default;
                }
            }
        }
    }
}
