using System.Collections;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Shooter.PlayMode.Tests
{
    public sealed class ShooterSynchronizationPlayModeSmokeTests
    {
        [TearDown]
        public void TearDown()
        {
            DemoLaunchIntent.Clear();
        }

        [UnityTest]
        public IEnumerator DefaultLaunchSpecAndUnifiedRequestUseMassBattleSyncInPlayMode()
        {
            yield return null;

            Assert.IsTrue(Application.isPlaying);

            var launchRequest = new DemoLaunchRequest(
                DemoGameplayId.Shooter,
                DemoLaunchMode.Multiplayer);
            DemoLaunchIntent.Request(in launchRequest);
            Assert.IsTrue(DemoLaunchIntent.TryConsume(out var consumedRequest));
            Assert.AreEqual(DemoGameplayId.Shooter, consumedRequest.Gameplay);
            Assert.AreEqual(DemoLaunchMode.Multiplayer, consumedRequest.Mode);

            ShooterRoomLaunchSpec spec = ShooterRoomLaunchSpec.CreateDefault("unity-playmode-smoke");
            Assert.AreEqual(ShooterSyncTemplateIds.MassBattleLodAoi, spec.SyncTemplateId);
            Assert.AreEqual((int)NetworkSyncModel.MassBattleLodSync, spec.SyncModel);
        }
    }
}
