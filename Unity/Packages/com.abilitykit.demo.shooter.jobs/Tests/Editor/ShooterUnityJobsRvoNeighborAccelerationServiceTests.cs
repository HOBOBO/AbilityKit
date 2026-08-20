#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.Jobs.Tests
{
    public sealed class ShooterUnityJobsRvoNeighborAccelerationServiceTests
    {
        [Test]
        public void CollectsSameStableTopNeighborsAsReferenceAcrossGridCells()
        {
            var entityIds = new uint[] { 50, 10, 40, 20, 30, 60, 70, 80 };
            var positionX = new[] { -1.01f, -0.99f, 0f, 0.99f, 1.01f, 0f, -0.2f, 0.2f };
            var positionY = new[] { 0f, 0f, 0f, 0f, 0f, 1.01f, -0.2f, -0.2f };

            using (var service = new ShooterUnityJobsRvoNeighborAccelerationService(
                       minimumAgentCount: 0,
                       innerLoopBatchCount: 1))
            {
                AssertMatchesReference(service, entityIds, positionX, positionY, 3, 1f);
            }
        }

        [Test]
        public void ReusesAndGrowsPersistentBuffersWithoutRetainingPreviousFrameEntries()
        {
            using (var service = new ShooterUnityJobsRvoNeighborAccelerationService(
                       minimumAgentCount: 0,
                       innerLoopBatchCount: 4))
            {
                AssertMatchesReference(
                    service,
                    new uint[] { 4, 3, 2, 1 },
                    new[] { 0f, 0.1f, 0.2f, 0.3f },
                    new[] { 0f, 0f, 0f, 0f },
                    2,
                    0.25f);

                const int count = 130;
                var entityIds = new uint[count];
                var positionX = new float[count];
                var positionY = new float[count];
                for (var index = 0; index < count; index++)
                {
                    entityIds[index] = (uint)(count - index);
                    positionX[index] = (index % 13) * 0.35f - 2f;
                    positionY[index] = (index / 13) * 0.35f - 2f;
                }

                AssertMatchesReference(service, entityIds, positionX, positionY, 6, 0.6f);
            }
        }

        [Test]
        public void CollectsStableNearestNeighborsInDenseTwoThousandAgentCrowd()
        {
            const int count = 2048;
            const int maxNeighbors = 12;
            const float spacing = 0.03125f;
            var entityIds = new uint[count];
            var positionX = new float[count];
            var positionY = new float[count];
            for (var index = 0; index < count; index++)
            {
                entityIds[index] = (uint)(count - index);
                positionX[index] = (index % 64) * spacing;
                positionY[index] = (index / 64) * spacing;
            }

            var actual = CreateBatch(
                entityIds,
                positionX,
                positionY,
                maxNeighbors,
                ShooterRvoOptions.DefaultNeighborDistance);
            using (var service = new ShooterUnityJobsRvoNeighborAccelerationService(
                       minimumAgentCount: 0,
                       innerLoopBatchCount: 16))
            {
                Assert.True(service.TryCollectNeighbors(in actual));

                var stopwatch = new System.Diagnostics.Stopwatch();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var allFramesCollected = true;
                stopwatch.Start();
                for (var frame = 0; frame < 5; frame++)
                {
                    allFramesCollected &= service.TryCollectNeighbors(in actual);
                }

                stopwatch.Stop();
                var steadyStateAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                TestContext.WriteLine(
                    $"Dense 2k steady state: {stopwatch.Elapsed.TotalMilliseconds:F3} ms / 5 frames, " +
                    $"{steadyStateAllocatedBytes} managed bytes.");
                Assert.True(allFramesCollected);
                Assert.LessOrEqual(steadyStateAllocatedBytes, 256L);
            }

            for (var agentIndex = 0; agentIndex < count; agentIndex++)
            {
                Assert.AreEqual(maxNeighbors, actual.NeighborCounts[agentIndex]);
                AssertNearestNeighborsAreStable(in actual, agentIndex);
            }

            AssertAgentMatchesReference(in actual, 0);
            AssertAgentMatchesReference(in actual, 31);
            AssertAgentMatchesReference(in actual, 32);
            AssertAgentMatchesReference(in actual, 1024);
            AssertAgentMatchesReference(in actual, count - 1);
        }

        [Test]
        public void CollectsStableNearestNeighborsInSparseTwoThousandAgentCrowd()
        {
            const int count = 2048;
            const int maxNeighbors = 12;
            const float spacing = 1.5f;
            var entityIds = new uint[count];
            var positionX = new float[count];
            var positionY = new float[count];
            for (var index = 0; index < count; index++)
            {
                entityIds[index] = (uint)(count - index);
                positionX[index] = (index % 64) * spacing;
                positionY[index] = (index / 64) * spacing;
            }

            var actual = CreateBatch(
                entityIds,
                positionX,
                positionY,
                maxNeighbors,
                ShooterRvoOptions.DefaultNeighborDistance);
            using (var service = new ShooterUnityJobsRvoNeighborAccelerationService(
                       minimumAgentCount: 0,
                       innerLoopBatchCount: 16))
            {
                Assert.True(service.TryCollectNeighbors(in actual));

                var stopwatch = new System.Diagnostics.Stopwatch();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var allFramesCollected = true;
                stopwatch.Start();
                for (var frame = 0; frame < 5; frame++)
                {
                    allFramesCollected &= service.TryCollectNeighbors(in actual);
                }

                stopwatch.Stop();
                var steadyStateAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                TestContext.WriteLine(
                    $"Sparse 2k steady state: {stopwatch.Elapsed.TotalMilliseconds:F3} ms / 5 frames, " +
                    $"{steadyStateAllocatedBytes} managed bytes.");
                Assert.True(allFramesCollected);
                Assert.LessOrEqual(steadyStateAllocatedBytes, 256L);
            }

            for (var agentIndex = 0; agentIndex < count; agentIndex++)
            {
                AssertNearestNeighborsAreStable(in actual, agentIndex);
            }

            AssertAgentMatchesReference(in actual, 0);
            AssertAgentMatchesReference(in actual, 31);
            AssertAgentMatchesReference(in actual, 32);
            AssertAgentMatchesReference(in actual, 1024);
            AssertAgentMatchesReference(in actual, count - 1);
        }

        [Test]
        public void RejectsInvalidInputAndBecomesUnavailableAfterDispose()
        {
            var service = new ShooterUnityJobsRvoNeighborAccelerationService(minimumAgentCount: 0);
            Assert.False(service.TryCollectNeighbors(default));

            var invalidPositionBatch = CreateBatch(
                new uint[] { 1, 2 },
                new[] { 0f, float.NaN },
                new[] { 0f, 0f },
                1,
                1f);
            Assert.False(service.TryCollectNeighbors(in invalidPositionBatch));

            var inverseOverflowBatch = CreateBatch(
                new uint[] { 1 },
                new[] { 0f },
                new[] { 0f },
                1,
                float.Epsilon);
            Assert.False(service.TryCollectNeighbors(in inverseOverflowBatch));

            var rangeSquaredOverflowBatch = CreateBatch(
                new uint[] { 1 },
                new[] { 0f },
                new[] { 0f },
                1,
                float.MaxValue);
            Assert.False(service.TryCollectNeighbors(in rangeSquaredOverflowBatch));

            var cellCoordinateOverflowBatch = CreateBatch(
                new uint[] { 1 },
                new[] { float.MaxValue },
                new[] { 0f },
                1,
                1f);
            Assert.False(service.TryCollectNeighbors(in cellCoordinateOverflowBatch));

            service.Dispose();
            Assert.False(service.IsAvailable);
            Assert.False(service.TryCollectNeighbors(in invalidPositionBatch));
        }

        private static void AssertMatchesReference(
            ShooterUnityJobsRvoNeighborAccelerationService service,
            uint[] entityIds,
            float[] positionX,
            float[] positionY,
            int maxNeighbors,
            float neighborDistance)
        {
            var actual = CreateBatch(
                entityIds,
                positionX,
                positionY,
                maxNeighbors,
                neighborDistance);
            Assert.True(service.TryCollectNeighbors(in actual));

            var expected = CreateBatch(
                entityIds,
                positionX,
                positionY,
                maxNeighbors,
                neighborDistance);
            CollectReference(in expected);

            CollectionAssert.AreEqual(expected.NeighborCounts, actual.NeighborCounts);
            for (var agentIndex = 0; agentIndex < actual.Count; agentIndex++)
            {
                var offset = agentIndex * maxNeighbors;
                for (var slot = 0; slot < actual.NeighborCounts[agentIndex]; slot++)
                {
                    Assert.AreEqual(
                        expected.NeighborIndices[offset + slot],
                        actual.NeighborIndices[offset + slot]);
                    Assert.AreEqual(
                        expected.NeighborDistanceSquared[offset + slot],
                        actual.NeighborDistanceSquared[offset + slot]);
                }
            }
        }

        private static ShooterRvoNeighborBatch CreateBatch(
            uint[] entityIds,
            float[] positionX,
            float[] positionY,
            int maxNeighbors,
            float neighborDistance)
        {
            var neighborCapacity = checked(entityIds.Length * maxNeighbors);
            return new ShooterRvoNeighborBatch(
                entityIds.Length,
                maxNeighbors,
                neighborDistance,
                entityIds,
                positionX,
                positionY,
                new int[entityIds.Length],
                new int[neighborCapacity],
                new float[neighborCapacity]);
        }

        private static void CollectReference(in ShooterRvoNeighborBatch batch)
        {
            var rangeSquared = batch.NeighborDistance * batch.NeighborDistance;
            for (var agentIndex = 0; agentIndex < batch.Count; agentIndex++)
            {
                var candidates = new List<Neighbor>();
                for (var candidateIndex = 0; candidateIndex < batch.Count; candidateIndex++)
                {
                    if (candidateIndex == agentIndex)
                    {
                        continue;
                    }

                    var deltaX = batch.PositionX[candidateIndex] - batch.PositionX[agentIndex];
                    var deltaY = batch.PositionY[candidateIndex] - batch.PositionY[agentIndex];
                    var distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared <= rangeSquared)
                    {
                        candidates.Add(new Neighbor(
                            candidateIndex,
                            batch.EntityIds[candidateIndex],
                            distanceSquared));
                    }
                }

                candidates.Sort();
                var count = Math.Min(batch.MaxNeighbors, candidates.Count);
                batch.NeighborCounts[agentIndex] = count;
                var offset = agentIndex * batch.MaxNeighbors;
                for (var slot = 0; slot < count; slot++)
                {
                    batch.NeighborIndices[offset + slot] = candidates[slot].Index;
                    batch.NeighborDistanceSquared[offset + slot] = candidates[slot].DistanceSquared;
                }
            }
        }

        private static void AssertNearestNeighborsAreStable(
            in ShooterRvoNeighborBatch batch,
            int agentIndex)
        {
            var offset = agentIndex * batch.MaxNeighbors;
            var previousDistance = -1f;
            var previousEntityId = 0u;
            for (var slot = 0; slot < batch.NeighborCounts[agentIndex]; slot++)
            {
                var neighborIndex = batch.NeighborIndices[offset + slot];
                var distance = batch.NeighborDistanceSquared[offset + slot];
                var entityId = batch.EntityIds[neighborIndex];
                Assert.AreNotEqual(agentIndex, neighborIndex);
                Assert.GreaterOrEqual(distance, previousDistance);
                if (distance == previousDistance)
                {
                    Assert.Greater(entityId, previousEntityId);
                }

                previousDistance = distance;
                previousEntityId = entityId;
            }
        }

        private static void AssertAgentMatchesReference(
            in ShooterRvoNeighborBatch batch,
            int agentIndex)
        {
            var expected = new List<Neighbor>(batch.Count - 1);
            for (var candidateIndex = 0; candidateIndex < batch.Count; candidateIndex++)
            {
                if (candidateIndex == agentIndex)
                {
                    continue;
                }

                var deltaX = batch.PositionX[candidateIndex] - batch.PositionX[agentIndex];
                var deltaY = batch.PositionY[candidateIndex] - batch.PositionY[agentIndex];
                var distanceSquared = deltaX * deltaX + deltaY * deltaY;
                if (distanceSquared <= batch.NeighborDistance * batch.NeighborDistance)
                {
                    expected.Add(new Neighbor(
                        candidateIndex,
                        batch.EntityIds[candidateIndex],
                        distanceSquared));
                }
            }

            expected.Sort();
            var offset = agentIndex * batch.MaxNeighbors;
            Assert.AreEqual(Math.Min(batch.MaxNeighbors, expected.Count), batch.NeighborCounts[agentIndex]);
            for (var slot = 0; slot < batch.NeighborCounts[agentIndex]; slot++)
            {
                Assert.AreEqual(expected[slot].Index, batch.NeighborIndices[offset + slot]);
                Assert.AreEqual(expected[slot].DistanceSquared, batch.NeighborDistanceSquared[offset + slot]);
            }
        }

        private readonly struct Neighbor : IComparable<Neighbor>
        {
            public Neighbor(int index, uint entityId, float distanceSquared)
            {
                Index = index;
                EntityId = entityId;
                DistanceSquared = distanceSquared;
            }

            public int Index { get; }
            public uint EntityId { get; }
            public float DistanceSquared { get; }

            public int CompareTo(Neighbor other)
            {
                var distanceOrder = DistanceSquared.CompareTo(other.DistanceSquared);
                if (distanceOrder != 0)
                {
                    return distanceOrder;
                }

                var entityOrder = EntityId.CompareTo(other.EntityId);
                return entityOrder != 0 ? entityOrder : Index.CompareTo(other.Index);
            }
        }
    }
}
