using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaProjectileLinkServiceDiagnosticsTests
    {
        [Test]
        public void CopyDiagnosticsTo_AppendsActiveLinksWithSourceSnapshotsAndDropsUnlinkedProjectile()
        {
            var service = new MobaProjectileLinkService();
            var sourcedProjectileId = new ProjectileId(1001);
            var unboundProjectileId = new ProjectileId(1002);
            var skillRuntimeHandle = new MobaSkillCastRuntimeHandle(91, 3, 7001L);
            var origin = default(MobaGameplayOrigin);
            var source = new ProjectileSourceContext(
                11,
                22,
                301,
                7002L,
                7001L,
                7003L,
                in skillRuntimeHandle,
                in origin);

            service.Link(sourcedProjectileId, 401);
            service.BindSource(sourcedProjectileId, in source);
            service.Link(unboundProjectileId, 402);

            var results = new List<MobaProjectileLinkDiagnostics>
            {
                default,
            };
            var copied = service.CopyDiagnosticsTo(results);

            Assert.That(copied, Is.EqualTo(2));
            Assert.That(results, Has.Count.EqualTo(3));

            var sourced = FindByProjectileId(results, sourcedProjectileId);
            Assert.That(sourced.ActorId, Is.EqualTo(401));
            Assert.That(sourced.HasSource, Is.True);
            Assert.That(sourced.Source.SourceActorId, Is.EqualTo(11));
            Assert.That(sourced.Source.InitialTargetActorId, Is.EqualTo(22));
            Assert.That(sourced.Source.ProjectileConfigId, Is.EqualTo(301));
            Assert.That(sourced.Source.SourceContextId, Is.EqualTo(7002L));
            Assert.That(sourced.Source.RootContextId, Is.EqualTo(7001L));
            Assert.That(sourced.Source.OwnerContextId, Is.EqualTo(7003L));
            Assert.That(sourced.Source.SkillRuntimeHandle, Is.EqualTo(skillRuntimeHandle));

            var unbound = FindByProjectileId(results, unboundProjectileId);
            Assert.That(unbound.ActorId, Is.EqualTo(402));
            Assert.That(unbound.HasSource, Is.False);

            service.UnlinkByProjectileId(sourcedProjectileId);
            results.Clear();
            copied = service.CopyDiagnosticsTo(results);

            Assert.That(copied, Is.EqualTo(1));
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].ProjectileId, Is.EqualTo(unboundProjectileId));
            Assert.That(service.CopyDiagnosticsTo(null), Is.Zero);
        }

        private static MobaProjectileLinkDiagnostics FindByProjectileId(
            IReadOnlyList<MobaProjectileLinkDiagnostics> entries,
            ProjectileId projectileId)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].ProjectileId.Value == projectileId.Value) return entries[i];
            }

            Assert.Fail($"Expected projectile {projectileId.Value}.");
            return default;
        }
    }
}
