using System;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Pure-logic tests for the position interpolation pipeline. These
    /// run as part of Unity EditMode tests (NUnit). They do not require
    /// a live scene.
    /// </summary>
    public sealed class BattleViewPositionSampleBufferPureTests
    {
        [Test]
        public void LinearInterpolation_BetweenSamples()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: Vector3.zero);
            buffer.Add(time: 1.0d, pos: new Vector3(10f, 0f, 0f));

            Assert.IsTrue(buffer.TryEvaluate(0.5d, out var pos));
            Assert.AreEqual(5f, pos.x, 0.0001f);
        }

        [Test]
        public void HoldsLastPosition_WithOnlyOneSample()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: new Vector3(2f, 3f, 4f));

            Assert.IsTrue(buffer.TryEvaluate(5.0d, out var pos));
            Assert.AreEqual(new Vector3(2f, 3f, 4f), pos);
        }

        [Test]
        public void ExtrapolatesBeyondLastSample_UsingLinearVelocity()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: Vector3.zero);
            buffer.Add(time: 1.0d, pos: new Vector3(10f, 0f, 0f));

            Assert.IsTrue(buffer.TryEvaluate(1.25d, out var pos));
            Assert.AreEqual(12.5f, pos.x, 0.0001f);
        }

        [Test]
        public void ExtrapolationClampsAtMaxLeadTime()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: Vector3.zero);
            buffer.Add(time: 1.0d, pos: new Vector3(10f, 0f, 0f));

            Assert.IsTrue(buffer.TryEvaluate(101.0d, out var pos));
            var clampedX = 10f + 10f * (1f / 15f);
            Assert.AreEqual(clampedX, pos.x, 0.05f);
        }

        [Test]
        public void CapacityIsAtLeastEight_RetainsRecentSamples()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            for (var i = 0; i < 10; i++)
            {
                buffer.Add(time: i, pos: new Vector3(i, 0f, 0f));
            }

            Assert.IsTrue(buffer.TryEvaluate(2d, out var firstPos));
            Assert.AreEqual(2f, firstPos.x, 0.0001f);

            Assert.IsTrue(buffer.TryEvaluate(9d, out var lastPos));
            Assert.AreEqual(9f, lastPos.x, 0.0001f);
        }

        [Test]
        public void FullBuffer_OutOfOrderMiddleSample_RemainsSortedAndIsEvaluated()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            for (var i = 0; i < 8; i++)
            {
                buffer.Add(time: i, pos: new Vector3(i, 0f, 0f));
            }

            buffer.Add(time: 3.5d, pos: new Vector3(35f, 0f, 0f));

            Assert.IsTrue(buffer.TryEvaluate(3.5d, out var exact));
            Assert.AreEqual(35f, exact.x, 0.0001f);
            Assert.IsTrue(buffer.TryEvaluate(3.75d, out var interpolated));
            Assert.AreEqual(19.5f, interpolated.x, 0.0001f);
        }
    }

    public sealed class BattleViewHandleStoreTests
    {
        [Test]
        public void RemovingReplacedEntity_PreservesCurrentActorMapping()
        {
            var store = new BattleViewHandleStore();
            var oldEntity = new IEntityId(1, 1);
            var replacementEntity = new IEntityId(2, 1);
            var oldHandle = store.GetOrCreate(oldEntity);
            var replacementHandle = store.GetOrCreate(replacementEntity);
            store.SetActorId(oldHandle, 1001, oldEntity);
            store.SetActorId(replacementHandle, 1001, replacementEntity);

            store.Remove(oldEntity);

            Assert.IsTrue(store.TryGetByActorId(1001, out var mappedEntity, out var mappedHandle));
            Assert.AreEqual(replacementEntity, mappedEntity);
            Assert.AreSame(replacementHandle, mappedHandle);
        }

        [Test]
        public void ReassigningReplacedEntity_PreservesCurrentActorMapping()
        {
            var store = new BattleViewHandleStore();
            var oldEntity = new IEntityId(1, 1);
            var replacementEntity = new IEntityId(2, 1);
            var oldHandle = store.GetOrCreate(oldEntity);
            var replacementHandle = store.GetOrCreate(replacementEntity);
            store.SetActorId(oldHandle, 1001, oldEntity);
            store.SetActorId(replacementHandle, 1001, replacementEntity);

            store.SetActorId(oldHandle, 2001, oldEntity);

            Assert.IsTrue(store.TryGetByActorId(1001, out var mappedEntity, out var mappedHandle));
            Assert.AreEqual(replacementEntity, mappedEntity);
            Assert.AreSame(replacementHandle, mappedHandle);
            Assert.IsTrue(store.TryGetByActorId(2001, out mappedEntity, out mappedHandle));
            Assert.AreEqual(oldEntity, mappedEntity);
            Assert.AreSame(oldHandle, mappedHandle);
        }
    }
}
