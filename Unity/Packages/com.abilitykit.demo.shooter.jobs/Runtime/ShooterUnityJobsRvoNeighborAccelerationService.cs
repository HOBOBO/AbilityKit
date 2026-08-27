#nullable enable

using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace AbilityKit.Demo.Shooter.Jobs
{
    public sealed class ShooterUnityJobsRvoNeighborAccelerationService :
        IShooterRvoNeighborAccelerationService,
        IShooterRvoAgentSolveAccelerationService,
        IDisposable
    {
        public const int DefaultMinimumAgentCount = 64;
        public const int DefaultInnerLoopBatchCount = 32;
        public const int DefaultMinimumParallelSolveAgentCount = 256;
        public const int DefaultMaximumSolveDegreeOfParallelism = 4;

        private const int DefaultGridCellDivisor = 4;
        private const int MediumDensityGridCellDivisor = 8;
        private const int HighDensityGridCellDivisor = 16;
        private const int HighDensityAgentCount = 1024;
        private const int MediumDensityTargetNeighborMultiplier = 2;
        private const int HighDensityTargetNeighborMultiplier = 8;

        // Leaves enough integer headroom for the fixed grid-neighborhood query.
        private const float MaximumCellCoordinateMagnitude = 2147483000f;

        private readonly int _minimumAgentCount;
        private readonly int _innerLoopBatchCount;
        private NativeArray<uint> _entityIds;
        private NativeArray<float> _positionX;
        private NativeArray<float> _positionY;
        private NativeArray<int> _neighborCounts;
        private NativeArray<int> _neighborIndices;
        private NativeArray<float> _neighborDistanceSquared;
        private NativeParallelMultiHashMap<long, int> _spatialGrid;
        private bool _disposed;

        public ShooterUnityJobsRvoNeighborAccelerationService(
            int minimumAgentCount = DefaultMinimumAgentCount,
            int innerLoopBatchCount = DefaultInnerLoopBatchCount)
        {
            if (minimumAgentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAgentCount));
            }

            if (innerLoopBatchCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(innerLoopBatchCount));
            }

            _minimumAgentCount = minimumAgentCount;
            _innerLoopBatchCount = innerLoopBatchCount;
        }

        public bool IsAvailable => !_disposed;

        /// <summary>
        /// 邻居收集用上面的 Burst jobs；ORCA 逐 agent 求解是纯数学、无 Unity API，
        /// 用 Parallel.For 并行（阈值与共享 ShooterParallelRvoAccelerationService 一致），
        /// 使托管求解器在 Unity 客户端也能并行解 ORCA。
        /// </summary>
        public bool TryForEachAgent(int count, Action<int> solveAgent)
        {
            if (_disposed || count < DefaultMinimumParallelSolveAgentCount ||
                Environment.ProcessorCount <= 1)
            {
                return false;
            }

            if (solveAgent == null)
            {
                throw new ArgumentNullException(nameof(solveAgent));
            }

            var degreeOfParallelism = Math.Min(DefaultMaximumSolveDegreeOfParallelism, Environment.ProcessorCount);
            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism }, solveAgent);
            return true;
        }

        public bool TryCollectNeighbors(in ShooterRvoNeighborBatch batch)
        {
            if (_disposed || batch.Count < _minimumAgentCount)
            {
                return false;
            }

            if (!ValidateBatch(in batch, out var neighborCapacity))
            {
                return false;
            }

            if (batch.Count == 0)
            {
                return true;
            }

            var gridCellDivisor = DetermineGridCellDivisor(in batch);
            var cellSize = batch.NeighborDistance / gridCellDivisor;
            var inverseCellSize = 1f / cellSize;
            var rangeSquared = batch.NeighborDistance * batch.NeighborDistance;
            if (!ValidateDerivedGridValues(in batch, inverseCellSize, rangeSquared))
            {
                return false;
            }

            EnsureCapacity(batch.Count, neighborCapacity);
            CopyInputs(in batch);
            _spatialGrid.Clear();

            var pendingHandle = default(JobHandle);
            var hasPendingHandle = false;
            try
            {
                pendingHandle = new BuildSpatialGridJob
                {
                    InverseCellSize = inverseCellSize,
                    PositionX = _positionX,
                    PositionY = _positionY,
                    SpatialGrid = _spatialGrid.AsParallelWriter()
                }.Schedule(batch.Count, _innerLoopBatchCount);
                hasPendingHandle = true;

                pendingHandle = new CollectNeighborsJob
                {
                    MaxNeighbors = batch.MaxNeighbors,
                    GridSearchRadius = gridCellDivisor + 1,
                    CellSize = cellSize,
                    InverseCellSize = inverseCellSize,
                    RangeSquared = rangeSquared,
                    EntityIds = _entityIds,
                    PositionX = _positionX,
                    PositionY = _positionY,
                    SpatialGrid = _spatialGrid,
                    NeighborCounts = _neighborCounts,
                    NeighborIndices = _neighborIndices,
                    NeighborDistanceSquared = _neighborDistanceSquared
                }.Schedule(batch.Count, _innerLoopBatchCount, pendingHandle);

                pendingHandle.Complete();
                hasPendingHandle = false;
                NativeArray<int>.Copy(_neighborCounts, batch.NeighborCounts, batch.Count);
                NativeArray<int>.Copy(_neighborIndices, batch.NeighborIndices, neighborCapacity);
                NativeArray<float>.Copy(
                    _neighborDistanceSquared,
                    batch.NeighborDistanceSquared,
                    neighborCapacity);
                return true;
            }
            finally
            {
                if (hasPendingHandle)
                {
                    pendingHandle.Complete();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DisposeIfCreated(ref _entityIds);
            DisposeIfCreated(ref _positionX);
            DisposeIfCreated(ref _positionY);
            DisposeIfCreated(ref _neighborCounts);
            DisposeIfCreated(ref _neighborIndices);
            DisposeIfCreated(ref _neighborDistanceSquared);
            if (_spatialGrid.IsCreated)
            {
                _spatialGrid.Dispose();
            }

            _disposed = true;
        }

        private static bool ValidateBatch(
            in ShooterRvoNeighborBatch batch,
            out int neighborCapacity)
        {
            neighborCapacity = 0;
            if (batch.Count < 0 || batch.MaxNeighbors <= 0 ||
                batch.NeighborDistance <= 0f ||
                !IsFinite(batch.NeighborDistance) ||
                batch.EntityIds == null ||
                batch.PositionX == null ||
                batch.PositionY == null ||
                batch.NeighborCounts == null ||
                batch.NeighborIndices == null ||
                batch.NeighborDistanceSquared == null)
            {
                return false;
            }

            try
            {
                neighborCapacity = checked(batch.Count * batch.MaxNeighbors);
            }
            catch (OverflowException)
            {
                return false;
            }

            if (batch.EntityIds.Length < batch.Count ||
                batch.PositionX.Length < batch.Count ||
                batch.PositionY.Length < batch.Count ||
                batch.NeighborCounts.Length < batch.Count ||
                batch.NeighborIndices.Length < neighborCapacity ||
                batch.NeighborDistanceSquared.Length < neighborCapacity)
            {
                return false;
            }

            for (var index = 0; index < batch.Count; index++)
            {
                if (!IsFinite(batch.PositionX[index]) || !IsFinite(batch.PositionY[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateDerivedGridValues(
            in ShooterRvoNeighborBatch batch,
            float inverseCellSize,
            float rangeSquared)
        {
            if (!IsFinite(inverseCellSize) || !IsFinite(rangeSquared))
            {
                return false;
            }

            for (var index = 0; index < batch.Count; index++)
            {
                var cellX = batch.PositionX[index] * inverseCellSize;
                var cellY = batch.PositionY[index] * inverseCellSize;
                if (!IsFinite(cellX) || !IsFinite(cellY) ||
                    Math.Abs(cellX) > MaximumCellCoordinateMagnitude ||
                    Math.Abs(cellY) > MaximumCellCoordinateMagnitude)
                {
                    return false;
                }
            }

            return true;
        }

        private static int DetermineGridCellDivisor(in ShooterRvoNeighborBatch batch)
        {
            if (batch.Count < HighDensityAgentCount)
            {
                return DefaultGridCellDivisor;
            }

            var minimumX = batch.PositionX[0];
            var maximumX = minimumX;
            var minimumY = batch.PositionY[0];
            var maximumY = minimumY;
            for (var index = 1; index < batch.Count; index++)
            {
                minimumX = Math.Min(minimumX, batch.PositionX[index]);
                maximumX = Math.Max(maximumX, batch.PositionX[index]);
                minimumY = Math.Min(minimumY, batch.PositionY[index]);
                maximumY = Math.Max(maximumY, batch.PositionY[index]);
            }

            var width = Math.Max(batch.NeighborDistance, maximumX - minimumX);
            var height = Math.Max(batch.NeighborDistance, maximumY - minimumY);
            var defaultCellSize = batch.NeighborDistance / DefaultGridCellDivisor;
            var estimatedAgentsPerDefaultCell =
                batch.Count * defaultCellSize * defaultCellSize / (width * height);
            if (estimatedAgentsPerDefaultCell >=
                batch.MaxNeighbors * HighDensityTargetNeighborMultiplier)
            {
                return HighDensityGridCellDivisor;
            }

            return estimatedAgentsPerDefaultCell >=
                batch.MaxNeighbors * MediumDensityTargetNeighborMultiplier
                ? MediumDensityGridCellDivisor
                : DefaultGridCellDivisor;
        }

        private void CopyInputs(in ShooterRvoNeighborBatch batch)
        {
            NativeArray<uint>.Copy(batch.EntityIds, _entityIds, batch.Count);
            NativeArray<float>.Copy(batch.PositionX, _positionX, batch.Count);
            NativeArray<float>.Copy(batch.PositionY, _positionY, batch.Count);
        }

        private void EnsureCapacity(int count, int neighborCapacity)
        {
            EnsureCapacity(ref _entityIds, count);
            EnsureCapacity(ref _positionX, count);
            EnsureCapacity(ref _positionY, count);
            EnsureCapacity(ref _neighborCounts, count);
            EnsureCapacity(ref _neighborIndices, neighborCapacity);
            EnsureCapacity(ref _neighborDistanceSquared, neighborCapacity);

            if (!_spatialGrid.IsCreated)
            {
                _spatialGrid = new NativeParallelMultiHashMap<long, int>(
                    GetCapacity(count),
                    Allocator.Persistent);
            }
            else if (_spatialGrid.Capacity < count)
            {
                _spatialGrid.Capacity = GetCapacity(count);
            }
        }

        private static int GetCapacity(int required)
        {
            var capacity = 64;
            while (capacity < required)
            {
                capacity = checked(capacity * 2);
            }

            return capacity;
        }

        private static void EnsureCapacity<T>(ref NativeArray<T> array, int required)
            where T : struct
        {
            if (array.IsCreated && array.Length >= required)
            {
                return;
            }

            DisposeIfCreated(ref array);
            array = new NativeArray<T>(
                GetCapacity(required),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private static void DisposeIfCreated<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildSpatialGridJob : IJobParallelFor
        {
            public float InverseCellSize;

            [ReadOnly] public NativeArray<float> PositionX;
            [ReadOnly] public NativeArray<float> PositionY;
            public NativeParallelMultiHashMap<long, int>.ParallelWriter SpatialGrid;

            public void Execute(int agentIndex)
            {
                SpatialGrid.Add(
                    ComputeCellKey(PositionX[agentIndex], PositionY[agentIndex], InverseCellSize),
                    agentIndex);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
        private struct CollectNeighborsJob : IJobParallelFor
        {
            public int MaxNeighbors;
            public int GridSearchRadius;
            public float CellSize;
            public float InverseCellSize;
            public float RangeSquared;

            [ReadOnly] public NativeArray<uint> EntityIds;
            [ReadOnly] public NativeArray<float> PositionX;
            [ReadOnly] public NativeArray<float> PositionY;
            [ReadOnly] public NativeParallelMultiHashMap<long, int> SpatialGrid;
            [NativeDisableParallelForRestriction] public NativeArray<int> NeighborCounts;
            [NativeDisableParallelForRestriction] public NativeArray<int> NeighborIndices;
            [NativeDisableParallelForRestriction] public NativeArray<float> NeighborDistanceSquared;

            public void Execute(int agentIndex)
            {
                var count = 0;
                var selfX = PositionX[agentIndex];
                var selfY = PositionY[agentIndex];
                var rowOffset = agentIndex * MaxNeighbors;
                var originX = FloorToInt(selfX * InverseCellSize);
                var originY = FloorToInt(selfY * InverseCellSize);

                CollectCell(
                    agentIndex,
                    selfX,
                    selfY,
                    rowOffset,
                    originX,
                    originY,
                    ref count);

                // The center cell normally fills the bounded nearest-neighbor set in dense
                // crowds. Visit the remaining cells in expanding rings so their AABB distance
                // can reject whole buckets once they cannot improve the current result.
                for (var ring = 1; ring <= GridSearchRadius; ring++)
                {
                    if (count == MaxNeighbors && RingMinimumDistanceSquared(
                            selfX,
                            selfY,
                            originX,
                            originY,
                            ring) > NeighborDistanceSquared[rowOffset + MaxNeighbors - 1])
                    {
                        break;
                    }

                    var minimumX = originX - ring;
                    var maximumX = originX + ring;
                    var minimumY = originY - ring;
                    var maximumY = originY + ring;
                    for (var x = minimumX; x <= maximumX; x++)
                    {
                        CollectCell(
                            agentIndex,
                            selfX,
                            selfY,
                            rowOffset,
                            x,
                            minimumY,
                            ref count);
                        CollectCell(
                            agentIndex,
                            selfX,
                            selfY,
                            rowOffset,
                            x,
                            maximumY,
                            ref count);
                    }

                    for (var y = minimumY + 1; y < maximumY; y++)
                    {
                        CollectCell(
                            agentIndex,
                            selfX,
                            selfY,
                            rowOffset,
                            minimumX,
                            y,
                            ref count);
                        CollectCell(
                            agentIndex,
                            selfX,
                            selfY,
                            rowOffset,
                            maximumX,
                            y,
                            ref count);
                    }
                }

                NeighborCounts[agentIndex] = count;
            }

            private void CollectCell(
                int agentIndex,
                float selfX,
                float selfY,
                int rowOffset,
                int cellX,
                int cellY,
                ref int count)
            {
                if (count == MaxNeighbors && CellMinimumDistanceSquared(selfX, selfY, cellX, cellY) >
                    NeighborDistanceSquared[rowOffset + MaxNeighbors - 1])
                {
                    return;
                }

                if (!SpatialGrid.TryGetFirstValue(
                        CombineCellKey(cellX, cellY),
                        out var candidateIndex,
                        out var iterator))
                {
                    return;
                }

                do
                {
                    if (candidateIndex == agentIndex)
                    {
                        continue;
                    }

                    var deltaX = PositionX[candidateIndex] - selfX;
                    var deltaY = PositionY[candidateIndex] - selfY;
                    var distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared <= RangeSquared)
                    {
                        InsertNeighbor(
                            rowOffset,
                            candidateIndex,
                            distanceSquared,
                            ref count);
                    }
                }
                while (SpatialGrid.TryGetNextValue(out candidateIndex, ref iterator));
            }

            private void InsertNeighbor(
                int rowOffset,
                int candidateIndex,
                float distanceSquared,
                ref int count)
            {
                if (count == MaxNeighbors && !IsBefore(
                        candidateIndex,
                        distanceSquared,
                        NeighborIndices[rowOffset + MaxNeighbors - 1],
                        NeighborDistanceSquared[rowOffset + MaxNeighbors - 1]))
                {
                    return;
                }

                var insertionIndex = count;
                while (insertionIndex > 0 && IsBefore(
                    candidateIndex,
                    distanceSquared,
                    NeighborIndices[rowOffset + insertionIndex - 1],
                    NeighborDistanceSquared[rowOffset + insertionIndex - 1]))
                {
                    insertionIndex--;
                }

                if (insertionIndex >= MaxNeighbors)
                {
                    return;
                }

                var nextCount = count < MaxNeighbors ? count + 1 : count;
                for (var index = nextCount - 1; index > insertionIndex; index--)
                {
                    NeighborIndices[rowOffset + index] = NeighborIndices[rowOffset + index - 1];
                    NeighborDistanceSquared[rowOffset + index] =
                        NeighborDistanceSquared[rowOffset + index - 1];
                }

                NeighborIndices[rowOffset + insertionIndex] = candidateIndex;
                NeighborDistanceSquared[rowOffset + insertionIndex] = distanceSquared;
                count = nextCount;
            }

            private bool IsBefore(
                int candidateIndex,
                float candidateDistanceSquared,
                int existingIndex,
                float existingDistanceSquared)
            {
                if (candidateDistanceSquared < existingDistanceSquared)
                {
                    return true;
                }

                if (candidateDistanceSquared != existingDistanceSquared)
                {
                    return false;
                }

                var candidateEntityId = EntityIds[candidateIndex];
                var existingEntityId = EntityIds[existingIndex];
                return candidateEntityId < existingEntityId ||
                    (candidateEntityId == existingEntityId && candidateIndex < existingIndex);
            }

            private float CellMinimumDistanceSquared(float x, float y, int cellX, int cellY)
            {
                const float boundaryPadding = 0.00001f;
                var minimumX = cellX * CellSize - boundaryPadding;
                var minimumY = cellY * CellSize - boundaryPadding;
                var maximumX = minimumX + CellSize + boundaryPadding * 2f;
                var maximumY = minimumY + CellSize + boundaryPadding * 2f;
                var deltaX = x < minimumX ? minimumX - x : x > maximumX ? x - maximumX : 0f;
                var deltaY = y < minimumY ? minimumY - y : y > maximumY ? y - maximumY : 0f;
                return deltaX * deltaX + deltaY * deltaY;
            }

            private float RingMinimumDistanceSquared(
                float x,
                float y,
                int originX,
                int originY,
                int ring)
            {
                var minimumDistance = CellMinimumDistanceSquared(x, y, originX - ring, originY);
                minimumDistance = Math.Min(
                    minimumDistance,
                    CellMinimumDistanceSquared(x, y, originX + ring, originY));
                minimumDistance = Math.Min(
                    minimumDistance,
                    CellMinimumDistanceSquared(x, y, originX, originY - ring));
                return Math.Min(
                    minimumDistance,
                    CellMinimumDistanceSquared(x, y, originX, originY + ring));
            }
        }

        private static long ComputeCellKey(float x, float y, float inverseCellSize)
        {
            return CombineCellKey(
                FloorToInt(x * inverseCellSize),
                FloorToInt(y * inverseCellSize));
        }

        private static long CombineCellKey(int x, int y)
        {
            return ((long)(uint)x << 32) | (uint)y;
        }

        private static int FloorToInt(float value)
        {
            var truncated = (int)value;
            return value < truncated ? truncated - 1 : truncated;
        }
    }
}
