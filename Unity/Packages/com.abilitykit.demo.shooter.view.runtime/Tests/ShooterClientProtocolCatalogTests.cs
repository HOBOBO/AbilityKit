using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class ShooterClientProtocolCatalogTests
    {
        [Test]
        public void DefaultRoomLaunchSpecUsesCatalogDefaultTemplate()
        {
            var spec = ShooterRoomLaunchSpec.CreateDefault("unity-test");

            Assert.AreEqual(ShooterSyncTemplateIds.PredictRollbackAuthority, ShooterRoomLaunchSpec.DefaultSyncTemplateId);
            Assert.AreEqual(ShooterSyncTemplateIds.PredictRollbackAuthority, spec.SyncTemplateId);
            Assert.AreEqual((int)NetworkSyncModel.PredictRollback, spec.SyncModel);
            Assert.AreEqual("1", spec.Tags[ShooterRoomLaunchTagKeys.MinPlayers]);
        }

        [Test]
        public void CatalogContainsAllPublishedTemplateIds()
        {
            AssertTemplate(ShooterSyncTemplateIds.PredictRollbackAuthority, NetworkSyncModel.PredictRollback);
            AssertTemplate(ShooterSyncTemplateIds.AuthoritativeInterpolationPresentation, NetworkSyncModel.AuthoritativeInterpolation);
            AssertTemplate(ShooterSyncTemplateIds.BatchStateLowFrequency, NetworkSyncModel.BatchStateSync);
            AssertTemplate(ShooterSyncTemplateIds.MassBattleLodAoi, NetworkSyncModel.MassBattleLodSync);
            AssertTemplate(ShooterSyncTemplateIds.HybridHeroPrediction, NetworkSyncModel.HybridHeroPrediction);
            AssertTemplate(ShooterSyncTemplateIds.FastReconnectResume, NetworkSyncModel.FastReconnect);
        }

        [Test]
        public void RoomLaunchTagsUseSharedProtocolKeys()
        {
            var tags = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ShooterRoomLaunchTagKeys.SyncTemplateId] = ShooterSyncTemplateIds.PredictRollbackAuthority,
                [ShooterRoomLaunchTagKeys.SyncModel] = ((int)NetworkSyncModel.PredictRollback).ToString(),
                [ShooterRoomLaunchTagKeys.NetworkEnvironmentId] = "ideal",
                [ShooterRoomLaunchTagKeys.CarrierName] = "server",
                [ShooterRoomLaunchTagKeys.EnableAuthoritativeWorld] = bool.TrueString,
                [ShooterRoomLaunchTagKeys.InterpolationEnabled] = bool.FalseString,
                [ShooterRoomLaunchTagKeys.InputDelayFrames] = "0",
                [ShooterRoomLaunchTagKeys.RandomSeed] = "3901",
                [ShooterRoomLaunchTagKeys.DurationFrames] = "3600"
            };

            Assert.AreEqual(ShooterSyncTemplateIds.PredictRollbackAuthority, tags[ShooterRoomLaunchTagKeys.SyncTemplateId]);
            Assert.AreEqual(((int)NetworkSyncModel.PredictRollback).ToString(), tags[ShooterRoomLaunchTagKeys.SyncModel]);
            Assert.IsTrue(tags.ContainsKey(ShooterRoomLaunchTagKeys.DurationFrames));
        }

        [Test]
        public void FrameworkSnapshotPipelineReusesGatewayPayloadBytes()
        {
            var payload = new byte[] { 1, 2, 3, 4 };
            var snapshot = new ShooterGatewaySnapshot(
                worldId: 7UL,
                frame: 12,
                timestamp: 1.5d,
                serverTicks: 99L,
                isFullSnapshot: true,
                actors: Array.Empty<ShooterGatewayActorSnapshot>(),
                payloadOpCode: 123,
                payloadBytes: payload);

            var packet = ShooterFrameworkSnapshotPipeline.ToFramePacket(in snapshot);

            Assert.That(snapshot.PayloadBytes, Is.SameAs(payload));
            Assert.That(packet.Snapshot.HasValue, Is.True);
            Assert.That(packet.Snapshot.Value.Payload, Is.SameAs(payload),
                "Gateway payload should enter SnapshotPipeline without a serialize/deserialize round trip.");
        }

        private static void AssertTemplate(string templateId, NetworkSyncModel expectedModel)
        {
            var template = ShooterAcceptanceCatalog.GetSyncTemplate(templateId);

            Assert.AreEqual(templateId, template.Id);
            Assert.AreEqual(expectedModel, template.SyncModel);
        }
    }
}
