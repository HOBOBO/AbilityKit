using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudRespawnClockTests
    {
        [Test]
        public void Hud_respawn_clock_uses_generic_snapshots_without_a_logic_world()
        {
            var context = BattleContext.Rent();
            var snapshots = new FrameSnapshotDispatcher();
            var generation = context.BindSnapshotRouting(snapshots, null, null);
            GameplayRespawnSnapshotRoute.RegisterDecoder(snapshots);
            var root = new GameObject("HudTestRoot", typeof(RectTransform));
            var clock = new BattleHudRespawnClock();
            var controller = new BattleHudSnapshotController();
            try
            {
                Assert.IsFalse(context.TryGetRuntimeWorld(out _));
                clock.Bind((RectTransform)root.transform);
                clock.SetLocalActorId(12);
                Assert.IsTrue(controller.Bind(context, null, null, null,
                    (Action<PresentationCueData[]>)null,
                    gameplayRespawnReceived: clock.Apply));
                var label = root.transform.Find("RespawnClock/Remaining").GetComponent<Text>();

                Feed(1, new GameplayRespawnData(11, 5000), new GameplayRespawnData(12, 3200));
                Assert.IsTrue(label.transform.parent.gameObject.activeSelf);
                Assert.AreEqual("复活 4 秒", label.text);

                Feed(2, new GameplayRespawnData(12, 1900));
                Assert.AreEqual("复活 2 秒", label.text);

                Feed(3);
                Assert.IsFalse(label.transform.parent.gameObject.activeSelf);
            }
            finally
            {
                controller.Dispose();
                clock.Dispose();
                context.ClearSnapshotRouting(generation, snapshots);
                BattleContext.Return(context);
                snapshots.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }

            void Feed(int frame, params GameplayRespawnData[] entries)
            {
                var snapshot = new WorldStateSnapshot(GameplayRespawnSnapshotCodec.OpCode,
                    GameplayRespawnSnapshotCodec.Encode(entries));
                snapshots.Feed(new FramePacket(new WorldId("hud_respawn"),
                    new FrameIndex(frame), Array.Empty<PlayerInputCommand>(), snapshot));
            }
        }
    }
}
