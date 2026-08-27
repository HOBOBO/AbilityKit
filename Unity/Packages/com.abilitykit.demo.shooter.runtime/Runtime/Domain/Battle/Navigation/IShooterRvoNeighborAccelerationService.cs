#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public interface IShooterRvoNeighborAccelerationService
    {
        bool IsAvailable { get; }

        bool TryCollectNeighbors(in ShooterRvoNeighborBatch batch);
    }

    public interface IShooterRvoAgentSolveAccelerationService
    {
        bool TryForEachAgent(int count, Action<int> solveAgent);
    }

    public readonly struct ShooterRvoNeighborBatch
    {
        public ShooterRvoNeighborBatch(
            int count,
            int maxNeighbors,
            float neighborDistance,
            uint[] entityIds,
            float[] positionX,
            float[] positionY,
            int[] neighborCounts,
            int[] neighborIndices,
            float[] neighborDistanceSquared)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (maxNeighbors <= 0) throw new ArgumentOutOfRangeException(nameof(maxNeighbors));
            if (neighborDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(neighborDistance));

            Count = count;
            MaxNeighbors = maxNeighbors;
            NeighborDistance = neighborDistance;
            EntityIds = entityIds ?? throw new ArgumentNullException(nameof(entityIds));
            PositionX = positionX ?? throw new ArgumentNullException(nameof(positionX));
            PositionY = positionY ?? throw new ArgumentNullException(nameof(positionY));
            NeighborCounts = neighborCounts ?? throw new ArgumentNullException(nameof(neighborCounts));
            NeighborIndices = neighborIndices ?? throw new ArgumentNullException(nameof(neighborIndices));
            NeighborDistanceSquared = neighborDistanceSquared ?? throw new ArgumentNullException(nameof(neighborDistanceSquared));
        }

        public int Count { get; }
        public int MaxNeighbors { get; }
        public float NeighborDistance { get; }
        public uint[] EntityIds { get; }
        public float[] PositionX { get; }
        public float[] PositionY { get; }
        public int[] NeighborCounts { get; }
        public int[] NeighborIndices { get; }
        public float[] NeighborDistanceSquared { get; }
    }

    public sealed class ShooterNullRvoNeighborAccelerationService : IShooterRvoNeighborAccelerationService
    {
        public static ShooterNullRvoNeighborAccelerationService Instance { get; } = new ShooterNullRvoNeighborAccelerationService();

        private ShooterNullRvoNeighborAccelerationService()
        {
        }

        public bool IsAvailable => false;

        public bool TryCollectNeighbors(in ShooterRvoNeighborBatch batch)
        {
            return false;
        }
    }
}
