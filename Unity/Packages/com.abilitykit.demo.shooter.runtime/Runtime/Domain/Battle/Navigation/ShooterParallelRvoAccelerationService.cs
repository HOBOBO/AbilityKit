#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Demo.Shooter.Runtime
{
    /// <summary>
    /// 平台无关的 RVO 加速服务：排序网格 + Parallel.For 邻居收集，以及并行逐 agent ORCA 求解。
    /// 与托管求解器逐字节同序（排序网格键 + 稳定 tie-break + 不可变输入/不相交输出槽），
    /// 供客户端世界（编辑器/Player）与服务端共用；Unity 上可选被 Burst jobs 收集服务替换邻居收集部分。
    /// </summary>
    public class ShooterParallelRvoAccelerationService :
        IShooterRvoNeighborAccelerationService,
        IShooterRvoAgentSolveAccelerationService
    {
        public const int DefaultMinimumParallelAgentCount = 256;
        public const int DefaultMaximumDegreeOfParallelism = 4;

        private readonly int _minimumParallelAgentCount;
        private readonly ParallelOptions _parallelOptions;
        private readonly Action<int> _collectAgentAction;
        private GridEntry[] _gridEntries = Array.Empty<GridEntry>();
        private uint[] _entityIds = Array.Empty<uint>();
        private float[] _positionX = Array.Empty<float>();
        private float[] _positionY = Array.Empty<float>();
        private int[] _neighborCounts = Array.Empty<int>();
        private int[] _neighborIndices = Array.Empty<int>();
        private float[] _neighborDistanceSquared = Array.Empty<float>();
        private int _count;
        private int _maxNeighbors;
        private float _inverseCellSize;
        private float _rangeSquared;

        public ShooterParallelRvoAccelerationService(
            int minimumParallelAgentCount = DefaultMinimumParallelAgentCount,
            int maximumDegreeOfParallelism = DefaultMaximumDegreeOfParallelism)
        {
            if (minimumParallelAgentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumParallelAgentCount));
            }

            if (maximumDegreeOfParallelism <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDegreeOfParallelism));
            }

            _minimumParallelAgentCount = minimumParallelAgentCount;
            _parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(
                    maximumDegreeOfParallelism,
                    Math.Max(1, Environment.ProcessorCount))
            };
            _collectAgentAction = CollectAgentNeighbors;
        }

        public bool IsAvailable => true;

        public bool TryCollectNeighbors(in ShooterRvoNeighborBatch batch)
        {
            if (!ValidateBatch(in batch))
            {
                return false;
            }

            if (batch.Count == 0)
            {
                return true;
            }

            EnsureGridCapacity(batch.Count);
            BindBatch(in batch);
            Array.Clear(_neighborCounts, 0, _count);

            for (var i = 0; i < _count; i++)
            {
                _gridEntries[i] = new GridEntry(
                    ComputeCellKey(_positionX[i], _positionY[i], _inverseCellSize),
                    i);
            }

            Array.Sort(_gridEntries, 0, _count);
            if (_count >= _minimumParallelAgentCount && _parallelOptions.MaxDegreeOfParallelism > 1)
            {
                Parallel.For(0, _count, _parallelOptions, _collectAgentAction);
            }
            else
            {
                for (var agentIndex = 0; agentIndex < _count; agentIndex++)
                {
                    CollectAgentNeighbors(agentIndex);
                }
            }

            return true;
        }

        public bool TryForEachAgent(int count, Action<int> solveAgent)
        {
            if (count < _minimumParallelAgentCount || _parallelOptions.MaxDegreeOfParallelism <= 1)
            {
                return false;
            }

            if (solveAgent == null)
            {
                throw new ArgumentNullException(nameof(solveAgent));
            }

            Parallel.For(0, count, _parallelOptions, solveAgent);
            return true;
        }

        private static bool ValidateBatch(in ShooterRvoNeighborBatch batch)
        {
            if (batch.Count < 0 || batch.MaxNeighbors <= 0 ||
                batch.NeighborDistance <= 0f ||
                float.IsNaN(batch.NeighborDistance) ||
                float.IsInfinity(batch.NeighborDistance))
            {
                return false;
            }

            var neighborCapacity = (long)batch.Count * batch.MaxNeighbors;
            return batch.EntityIds.Length >= batch.Count &&
                batch.PositionX.Length >= batch.Count &&
                batch.PositionY.Length >= batch.Count &&
                batch.NeighborCounts.Length >= batch.Count &&
                batch.NeighborIndices.LongLength >= neighborCapacity &&
                batch.NeighborDistanceSquared.LongLength >= neighborCapacity;
        }

        private void BindBatch(in ShooterRvoNeighborBatch batch)
        {
            _count = batch.Count;
            _maxNeighbors = batch.MaxNeighbors;
            _inverseCellSize = 1f / batch.NeighborDistance;
            _rangeSquared = batch.NeighborDistance * batch.NeighborDistance;
            _entityIds = batch.EntityIds;
            _positionX = batch.PositionX;
            _positionY = batch.PositionY;
            _neighborCounts = batch.NeighborCounts;
            _neighborIndices = batch.NeighborIndices;
            _neighborDistanceSquared = batch.NeighborDistanceSquared;
        }

        private void CollectAgentNeighbors(int agentIndex)
        {
            var originX = FloorToInt(_positionX[agentIndex] * _inverseCellSize);
            var originY = FloorToInt(_positionY[agentIndex] * _inverseCellSize);
            for (var y = originY - 1; y <= originY + 1; y++)
            {
                for (var x = originX - 1; x <= originX + 1; x++)
                {
                    CollectCell(agentIndex, CombineCellKey(x, y));
                }
            }
        }

        private void CollectCell(int agentIndex, long cellKey)
        {
            var first = LowerBound(cellKey);
            for (var i = first; i < _count && _gridEntries[i].CellKey == cellKey; i++)
            {
                var candidateIndex = _gridEntries[i].AgentIndex;
                if (candidateIndex == agentIndex)
                {
                    continue;
                }

                var deltaX = _positionX[candidateIndex] - _positionX[agentIndex];
                var deltaY = _positionY[candidateIndex] - _positionY[agentIndex];
                var distanceSquared = deltaX * deltaX + deltaY * deltaY;
                if (distanceSquared <= _rangeSquared)
                {
                    InsertNeighbor(agentIndex, candidateIndex, distanceSquared);
                }
            }
        }

        private void InsertNeighbor(int agentIndex, int candidateIndex, float distanceSquared)
        {
            var count = _neighborCounts[agentIndex];
            var offset = agentIndex * _maxNeighbors;
            var insertIndex = count;
            while (insertIndex > 0)
            {
                var previousSlot = insertIndex - 1;
                var previousDistance = _neighborDistanceSquared[offset + previousSlot];
                var previousIndex = _neighborIndices[offset + previousSlot];
                if (previousDistance < distanceSquared ||
                    (previousDistance == distanceSquared &&
                     (_entityIds[previousIndex] < _entityIds[candidateIndex] ||
                      (_entityIds[previousIndex] == _entityIds[candidateIndex] && previousIndex < candidateIndex))))
                {
                    break;
                }

                insertIndex--;
            }

            if (insertIndex >= _maxNeighbors)
            {
                return;
            }

            var newCount = Math.Min(count + 1, _maxNeighbors);
            for (var slot = newCount - 1; slot > insertIndex; slot--)
            {
                _neighborIndices[offset + slot] = _neighborIndices[offset + slot - 1];
                _neighborDistanceSquared[offset + slot] = _neighborDistanceSquared[offset + slot - 1];
            }

            _neighborIndices[offset + insertIndex] = candidateIndex;
            _neighborDistanceSquared[offset + insertIndex] = distanceSquared;
            _neighborCounts[agentIndex] = newCount;
        }

        private int LowerBound(long cellKey)
        {
            var left = 0;
            var right = _count;
            while (left < right)
            {
                var middle = left + ((right - left) >> 1);
                if (_gridEntries[middle].CellKey < cellKey)
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

        private void EnsureGridCapacity(int count)
        {
            if (_gridEntries.Length >= count)
            {
                return;
            }

            var capacity = Math.Max(256, _gridEntries.Length);
            while (capacity < count)
            {
                capacity = checked(capacity * 2);
            }

            Array.Resize(ref _gridEntries, capacity);
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

        private readonly struct GridEntry : IComparable<GridEntry>
        {
            public GridEntry(long cellKey, int agentIndex)
            {
                CellKey = cellKey;
                AgentIndex = agentIndex;
            }

            public long CellKey { get; }
            public int AgentIndex { get; }

            public int CompareTo(GridEntry other)
            {
                var keyOrder = CellKey.CompareTo(other.CellKey);
                return keyOrder != 0 ? keyOrder : AgentIndex.CompareTo(other.AgentIndex);
            }
        }
    }
}
