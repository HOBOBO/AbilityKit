#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterRvoWorldWorkspace
    {
        private int _agentCapacity;
        private int _neighborCapacity;

        public ShooterRvoWorldWorkspace(int initialCapacity, int maxNeighbors)
        {
            _neighborCapacity = Math.Max(1, maxNeighbors);
            EnsureCapacity(Math.Max(1, initialCapacity));
        }

        public int Count { get; private set; }

        public uint[] EntityIds { get; private set; } = Array.Empty<uint>();
        public int[] SourceIndices { get; private set; } = Array.Empty<int>();
        public float[] PositionX { get; private set; } = Array.Empty<float>();
        public float[] PositionY { get; private set; } = Array.Empty<float>();
        public float[] VelocityX { get; private set; } = Array.Empty<float>();
        public float[] VelocityY { get; private set; } = Array.Empty<float>();
        public float[] PreferredVelocityX { get; private set; } = Array.Empty<float>();
        public float[] PreferredVelocityY { get; private set; } = Array.Empty<float>();
        public float[] OutputVelocityX { get; private set; } = Array.Empty<float>();
        public float[] OutputVelocityY { get; private set; } = Array.Empty<float>();
        public float[] Radius { get; private set; } = Array.Empty<float>();
        public float[] MaxSpeed { get; private set; } = Array.Empty<float>();
        public int[] NeighborCounts { get; private set; } = Array.Empty<int>();
        public int[] NeighborIndices { get; private set; } = Array.Empty<int>();
        public float[] NeighborDistanceSquared { get; private set; } = Array.Empty<float>();
        public ShooterRvoLine[] Lines { get; private set; } = Array.Empty<ShooterRvoLine>();
        public ShooterRvoLine[] ProjectedLines { get; private set; } = Array.Empty<ShooterRvoLine>();
        public ShooterRvoGridEntry[] GridEntries { get; private set; } = Array.Empty<ShooterRvoGridEntry>();

        public void BeginFrame(int count, int maxNeighbors)
        {
            _neighborCapacity = Math.Max(1, maxNeighbors);
            EnsureCapacity(Math.Max(1, count));
            Count = Math.Max(0, count);
            Array.Clear(PreferredVelocityX, 0, Count);
            Array.Clear(PreferredVelocityY, 0, Count);
            Array.Clear(OutputVelocityX, 0, Count);
            Array.Clear(OutputVelocityY, 0, Count);
            Array.Clear(NeighborCounts, 0, Count);
        }

        public void SortByEntityId()
        {
            Array.Sort(EntityIds, SourceIndices, 0, Count);
        }

        public void BuildGrid(float cellSize)
        {
            var inverseCellSize = 1f / cellSize;
            for (var i = 0; i < Count; i++)
            {
                GridEntries[i] = new ShooterRvoGridEntry(
                    ComputeCellKey(PositionX[i], PositionY[i], inverseCellSize),
                    i);
            }

            Array.Sort(GridEntries, 0, Count);
        }

        public void CollectNeighbors(int agentIndex, float neighborDistance)
        {
            var inverseCellSize = 1f / neighborDistance;
            var originX = FloorToInt(PositionX[agentIndex] * inverseCellSize);
            var originY = FloorToInt(PositionY[agentIndex] * inverseCellSize);
            var rangeSquared = neighborDistance * neighborDistance;

            for (var y = originY - 1; y <= originY + 1; y++)
            {
                for (var x = originX - 1; x <= originX + 1; x++)
                {
                    CollectCell(agentIndex, CombineCellKey(x, y), rangeSquared);
                }
            }
        }

        private void CollectCell(int agentIndex, long cellKey, float rangeSquared)
        {
            var first = LowerBound(cellKey);
            for (var i = first; i < Count && GridEntries[i].CellKey == cellKey; i++)
            {
                var candidateIndex = GridEntries[i].AgentIndex;
                if (candidateIndex == agentIndex)
                {
                    continue;
                }

                var dx = PositionX[candidateIndex] - PositionX[agentIndex];
                var dy = PositionY[candidateIndex] - PositionY[agentIndex];
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > rangeSquared)
                {
                    continue;
                }

                InsertNeighbor(agentIndex, candidateIndex, distanceSquared);
            }
        }

        private void InsertNeighbor(int agentIndex, int candidateIndex, float distanceSquared)
        {
            var count = NeighborCounts[agentIndex];
            var offset = agentIndex * _neighborCapacity;
            var insertIndex = count;
            while (insertIndex > 0)
            {
                var previous = insertIndex - 1;
                var previousDistance = NeighborDistanceSquared[offset + previous];
                var previousAgent = NeighborIndices[offset + previous];
                if (previousDistance < distanceSquared ||
                    (previousDistance == distanceSquared && EntityIds[previousAgent] < EntityIds[candidateIndex]))
                {
                    break;
                }

                insertIndex--;
            }

            if (insertIndex >= _neighborCapacity)
            {
                return;
            }

            var newCount = Math.Min(count + 1, _neighborCapacity);
            for (var i = newCount - 1; i > insertIndex; i--)
            {
                NeighborIndices[offset + i] = NeighborIndices[offset + i - 1];
                NeighborDistanceSquared[offset + i] = NeighborDistanceSquared[offset + i - 1];
            }

            NeighborIndices[offset + insertIndex] = candidateIndex;
            NeighborDistanceSquared[offset + insertIndex] = distanceSquared;
            NeighborCounts[agentIndex] = newCount;
        }

        private int LowerBound(long cellKey)
        {
            var left = 0;
            var right = Count;
            while (left < right)
            {
                var middle = left + ((right - left) >> 1);
                if (GridEntries[middle].CellKey < cellKey)
                {
                    left = middle + 1;
                }
                else
                {
                    right = middle;
                }
            }

            return left;
        }

        private void EnsureCapacity(int required)
        {
            var requiredNeighborSlots = checked(required * _neighborCapacity);
            if (required <= _agentCapacity && NeighborIndices.Length >= requiredNeighborSlots)
            {
                return;
            }

            var capacity = Math.Max(16, _agentCapacity);
            while (capacity < required)
            {
                capacity = checked(capacity * 2);
            }

            _agentCapacity = capacity;
            EntityIds = Resize(EntityIds, capacity);
            SourceIndices = Resize(SourceIndices, capacity);
            PositionX = Resize(PositionX, capacity);
            PositionY = Resize(PositionY, capacity);
            VelocityX = Resize(VelocityX, capacity);
            VelocityY = Resize(VelocityY, capacity);
            PreferredVelocityX = Resize(PreferredVelocityX, capacity);
            PreferredVelocityY = Resize(PreferredVelocityY, capacity);
            OutputVelocityX = Resize(OutputVelocityX, capacity);
            OutputVelocityY = Resize(OutputVelocityY, capacity);
            Radius = Resize(Radius, capacity);
            MaxSpeed = Resize(MaxSpeed, capacity);
            NeighborCounts = Resize(NeighborCounts, capacity);
            GridEntries = Resize(GridEntries, capacity);

            var neighborSlots = checked(capacity * _neighborCapacity);
            NeighborIndices = Resize(NeighborIndices, neighborSlots);
            NeighborDistanceSquared = Resize(NeighborDistanceSquared, neighborSlots);
            Lines = Resize(Lines, neighborSlots);
            // LinearProgram3 uses scratch lines. Keep one disjoint segment per
            // agent so a server accelerator can solve agents concurrently.
            ProjectedLines = Resize(ProjectedLines, neighborSlots);
        }

        private static T[] Resize<T>(T[] source, int size)
        {
            if (source.Length == size)
            {
                return source;
            }

            Array.Resize(ref source, size);
            return source;
        }

        private static long ComputeCellKey(float x, float y, float inverseCellSize)
        {
            return CombineCellKey(FloorToInt(x * inverseCellSize), FloorToInt(y * inverseCellSize));
        }

        private static long CombineCellKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private static int FloorToInt(float value)
        {
            var truncated = (int)value;
            return value < truncated ? truncated - 1 : truncated;
        }
    }

    internal readonly struct ShooterRvoGridEntry : IComparable<ShooterRvoGridEntry>
    {
        public ShooterRvoGridEntry(long cellKey, int agentIndex)
        {
            CellKey = cellKey;
            AgentIndex = agentIndex;
        }

        public long CellKey { get; }
        public int AgentIndex { get; }

        public int CompareTo(ShooterRvoGridEntry other)
        {
            var keyOrder = CellKey.CompareTo(other.CellKey);
            return keyOrder != 0 ? keyOrder : AgentIndex.CompareTo(other.AgentIndex);
        }
    }

    internal struct ShooterRvoLine
    {
        public ShooterRvoVector Point;
        public ShooterRvoVector Direction;
    }

    internal readonly struct ShooterRvoVector
    {
        public ShooterRvoVector(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public float LengthSquared => X * X + Y * Y;

        public static ShooterRvoVector operator +(ShooterRvoVector left, ShooterRvoVector right) => new(left.X + right.X, left.Y + right.Y);
        public static ShooterRvoVector operator -(ShooterRvoVector left, ShooterRvoVector right) => new(left.X - right.X, left.Y - right.Y);
        public static ShooterRvoVector operator -(ShooterRvoVector value) => new(-value.X, -value.Y);
        public static ShooterRvoVector operator *(ShooterRvoVector value, float scalar) => new(value.X * scalar, value.Y * scalar);
        public static ShooterRvoVector operator *(float scalar, ShooterRvoVector value) => value * scalar;
        public static ShooterRvoVector operator /(ShooterRvoVector value, float scalar) => new(value.X / scalar, value.Y / scalar);

        public static float Dot(ShooterRvoVector left, ShooterRvoVector right) => left.X * right.X + left.Y * right.Y;
        public static float Det(ShooterRvoVector left, ShooterRvoVector right) => left.X * right.Y - left.Y * right.X;

        public ShooterRvoVector NormalizedOr(ShooterRvoVector fallback)
        {
            var lengthSquared = LengthSquared;
            if (lengthSquared <= ShooterManagedRvoSolver.EpsilonSquared)
            {
                return fallback;
            }

            return this / MathF.Sqrt(lengthSquared);
        }
    }
}
