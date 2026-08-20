#nullable enable

using System;
using AbilityKit.Protocol.Shooter;
using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class ShooterControlledPlayerPredictionProjectionTests
    {
        [Test]
        public void ControlledPlayerPredictionDoesNotReintroduceDespawnedRemotePlayer()
        {
            var mapper = new ShooterSnapshotViewModelMapper();
            var projection = new ShooterSnapshotViewProjection();
            var localKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1);
            var remoteKey = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2);
            var initialSnapshot = CreateSnapshot(frame: 10, localX: 1f, remoteX: 2f);
            var initialBatch = mapper.Map(in initialSnapshot, ShooterViewBatchSource.LocalPrediction);

            try
            {
                projection.Apply(in initialBatch);
                Assert.That(projection.HasEntity(localKey), Is.True);
                Assert.That(projection.HasEntity(remoteKey), Is.True);
            }
            finally
            {
                initialBatch.ReleasePooledResources();
            }

            var despawnBatch = new ShooterSnapshotViewBatch(
                worldId: 17UL,
                frame: 11,
                sequence: 2UL,
                snapshotKind: ShooterViewSnapshotKind.Delta,
                source: ShooterViewBatchSource.AuthoritativeCorrection,
                entityChanges: Array.Empty<ShooterViewEntityChange>(),
                removedEntities: new[] { remoteKey },
                transformChanges: Array.Empty<ShooterViewTransformComponentChange>(),
                healthChanges: Array.Empty<ShooterViewHealthComponentChange>(),
                scoreChanges: Array.Empty<ShooterViewScoreComponentChange>(),
                projectileLifetimeChanges: Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
                events: Array.Empty<ShooterEventSnapshot>());
            projection.Apply(in despawnBatch);
            Assert.That(projection.HasEntity(remoteKey), Is.False);

            var predictedSnapshot = CreateSnapshot(frame: 12, localX: 3f, remoteX: 4f);
            var controlledBatch = mapper.MapControlledPlayerPrediction(in predictedSnapshot, controlledPlayerId: 1);

            try
            {
                Assert.That(controlledBatch.SnapshotKind, Is.EqualTo(ShooterViewSnapshotKind.Delta));
                Assert.That(controlledBatch.Source, Is.EqualTo(ShooterViewBatchSource.LocalPrediction));
                Assert.That(controlledBatch.EntityChanges, Has.Count.EqualTo(1));
                Assert.That(controlledBatch.EntityChanges[0].Key, Is.EqualTo(localKey));
                AssertAllChangesTarget(controlledBatch, localKey);

                var result = projection.Apply(in controlledBatch);
                Assert.That(projection.HasEntity(localKey), Is.True);
                Assert.That(projection.HasEntity(remoteKey), Is.False);
                Assert.That(result.FinalPlayerCount, Is.EqualTo(1));
            }
            finally
            {
                controlledBatch.ReleasePooledResources();
            }
        }

        private static ShooterStateSnapshotPayload CreateSnapshot(int frame, float localX, float remoteX)
        {
            return new ShooterStateSnapshotPayload(
                frame,
                new[]
                {
                    new ShooterPlayerSnapshot(1, localX, 0f, 1f, 0f, 100, 1, true),
                    new ShooterPlayerSnapshot(2, remoteX, 0f, -1f, 0f, 100, 2, true)
                },
                Array.Empty<ShooterBulletSnapshot>(),
                Array.Empty<ShooterEventSnapshot>());
        }

        private static void AssertAllChangesTarget(
            in ShooterSnapshotViewBatch batch,
            ShooterViewEntityKey expectedKey)
        {
            Assert.That(batch.RemovedEntities, Is.Empty);
            Assert.That(batch.ProjectileLifetimeChanges, Is.Empty);
            Assert.That(batch.Events, Is.Empty);
            Assert.That(batch.TransformChanges, Has.Count.EqualTo(1));
            Assert.That(batch.TransformChanges[0].Key, Is.EqualTo(expectedKey));
            Assert.That(batch.HealthChanges, Has.Count.EqualTo(1));
            Assert.That(batch.HealthChanges[0].Key, Is.EqualTo(expectedKey));
            Assert.That(batch.ScoreChanges, Has.Count.EqualTo(1));
            Assert.That(batch.ScoreChanges[0].Key, Is.EqualTo(expectedKey));
        }
    }
}
