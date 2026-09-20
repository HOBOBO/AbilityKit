using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class DebugBattleViewEventFormatter
    {
        public string FormatDamageResult(in DamageResult result)
        {
            return $"DamageResult: target={result.TargetActorId}";
        }

        public string FormatProjectileHit(in ProjectileHitEvent evt)
        {
            return $"ProjectileHit: projectile={evt.Projectile.Value}, template={evt.TemplateId}";
        }

        public string FormatEnterGame(in EnterMobaGameRes res)
        {
            return $"EnterGame: tickRate={res.TickRate}";
        }

        public string FormatActorTransforms(MobaActorTransformSnapshotEntry[] entries)
        {
            return entries != null ? $"Transform: n={entries.Length}" : null;
        }

        public string FormatProjectiles(MobaProjectileEventSnapshotEntry[] entries)
        {
            return entries != null ? $"Projectile: n={entries.Length}" : null;
        }

        public string FormatAreas(MobaAreaEventSnapshotEntry[] entries)
        {
            return entries != null ? $"Area: n={entries.Length}" : null;
        }

        public string FormatDamages(MobaDamageEventSnapshotEntry[] entries)
        {
            return entries != null ? $"Damage: n={entries.Length}" : null;
        }

        public string FormatPresentationCues(PresentationCueData[] entries)
        {
            return entries != null ? $"PresentationCue: n={entries.Length}" : null;
        }
    }
}
