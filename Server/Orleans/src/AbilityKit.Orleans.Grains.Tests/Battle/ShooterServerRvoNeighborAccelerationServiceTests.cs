using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ShooterServerRvoNeighborAccelerationServiceTests
{
    [Fact]
    public void CollectNeighbors_MatchesDeterministicBruteForceReference()
    {
        const int count = 513;
        const int maxNeighbors = 12;
        const float neighborDistance = 2.5f;
        var entityIds = new uint[count];
        var positionX = new float[count];
        var positionY = new float[count];
        for (var i = 0; i < count; i++)
        {
            entityIds[i] = (uint)(10000 + i * 7);
            positionX[i] = (i % 27) * 0.41f - 5f;
            positionY[i] = (i / 27) * 0.37f - 3f;
        }

        var actualCounts = new int[count];
        var actualIndices = new int[count * maxNeighbors];
        var actualDistances = new float[count * maxNeighbors];
        var batch = new ShooterRvoNeighborBatch(
            count,
            maxNeighbors,
            neighborDistance,
            entityIds,
            positionX,
            positionY,
            actualCounts,
            actualIndices,
            actualDistances);
        var service = new ShooterServerRvoNeighborAccelerationService(
            minimumParallelAgentCount: 1,
            maximumDegreeOfParallelism: 4);

        Assert.True(service.TryCollectNeighbors(in batch));

        var rangeSquared = neighborDistance * neighborDistance;
        for (var agentIndex = 0; agentIndex < count; agentIndex++)
        {
            var expected = Enumerable.Range(0, count)
                .Where(candidateIndex => candidateIndex != agentIndex)
                .Select(candidateIndex => new
                {
                    Index = candidateIndex,
                    Distance = DistanceSquared(positionX, positionY, agentIndex, candidateIndex)
                })
                .Where(candidate => candidate.Distance <= rangeSquared)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => entityIds[candidate.Index])
                .ThenBy(candidate => candidate.Index)
                .Take(maxNeighbors)
                .ToArray();
            Assert.Equal(expected.Length, actualCounts[agentIndex]);
            var offset = agentIndex * maxNeighbors;
            for (var slot = 0; slot < expected.Length; slot++)
            {
                Assert.Equal(expected[slot].Index, actualIndices[offset + slot]);
                Assert.Equal(expected[slot].Distance, actualDistances[offset + slot]);
            }
        }
    }

    [Fact]
    public void CollectNeighbors_ReusesBuffersAndProducesStableResults()
    {
        const int count = 300;
        const int maxNeighbors = 8;
        var entityIds = Enumerable.Range(1, count).Select(value => (uint)value).ToArray();
        var positionX = Enumerable.Range(0, count).Select(value => value % 20 * 0.5f).ToArray();
        var positionY = Enumerable.Range(0, count).Select(value => value / 20 * 0.5f).ToArray();
        var counts = new int[count];
        var indices = new int[count * maxNeighbors];
        var distances = new float[count * maxNeighbors];
        var batch = new ShooterRvoNeighborBatch(
            count,
            maxNeighbors,
            1.5f,
            entityIds,
            positionX,
            positionY,
            counts,
            indices,
            distances);
        var service = new ShooterServerRvoNeighborAccelerationService(
            minimumParallelAgentCount: 1,
            maximumDegreeOfParallelism: 4);

        Assert.True(service.TryCollectNeighbors(in batch));
        var expectedCounts = (int[])counts.Clone();
        var expectedIndices = (int[])indices.Clone();
        var expectedDistances = (float[])distances.Clone();
        Array.Fill(counts, -1);
        Array.Fill(indices, -1);
        Array.Fill(distances, -1f);

        Assert.True(service.TryCollectNeighbors(in batch));
        Assert.Equal(expectedCounts, counts);
        Assert.Equal(expectedIndices, indices);
        Assert.Equal(expectedDistances, distances);
    }

    [Fact]
    public void SolveAgents_UsesParallelPathOnlyAboveConfiguredThreshold()
    {
        var service = new ShooterServerRvoNeighborAccelerationService(
            minimumParallelAgentCount: 4,
            maximumDegreeOfParallelism: 4);
        var visits = new int[8];

        Assert.False(service.TryForEachAgent(3, index => visits[index]++));
        Assert.All(visits, value => Assert.Equal(0, value));

        Assert.True(service.TryForEachAgent(visits.Length, index => Interlocked.Increment(ref visits[index])));
        Assert.All(visits, value => Assert.Equal(1, value));
    }

    private static float DistanceSquared(
        float[] positionX,
        float[] positionY,
        int left,
        int right)
    {
        var deltaX = positionX[right] - positionX[left];
        var deltaY = positionY[right] - positionY[left];
        return deltaX * deltaX + deltaY * deltaY;
    }
}
