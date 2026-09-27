using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;

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

        public string FormatEnterGame(in BattleEnterGameSnapshot res)
        {
            return $"EnterGame: tickRate={res.TickRate}";
        }

        public string FormatActorTransforms(ActorTransformData[] entries)
        {
            return entries != null ? $"Transform: n={entries.Length}" : null;
        }

        public string FormatProjectiles(ProjectileEventData[] entries)
        {
            return entries != null ? $"Projectile: n={entries.Length}" : null;
        }

        public string FormatAreas(AreaEventData[] entries)
        {
            return entries != null ? $"Area: n={entries.Length}" : null;
        }

        public string FormatDamages(DamageEventData[] entries)
        {
            return entries != null ? $"Damage: n={entries.Length}" : null;
        }

        public string FormatPresentationCues(PresentationCueData[] entries)
        {
            return entries != null ? $"PresentationCue: n={entries.Length}" : null;
        }
    }
}
