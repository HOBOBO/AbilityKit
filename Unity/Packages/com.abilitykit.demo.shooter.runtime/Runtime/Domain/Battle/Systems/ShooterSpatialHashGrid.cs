#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterSpatialHashGrid
    {
        private readonly float _cellSize;
        private readonly Dictionary<long, ShooterSpatialHashCell> _cells = new();
        private readonly List<long> _activeCellKeys = new(128);
        private readonly List<ShooterSpatialSearchOffset> _ringOffsets = new(128);
        private readonly List<ShooterSpatialRingOffsetRange> _ringOffsetRanges = new(16);
        private int _largestCellOccupancy;
        private int _totalEntries;

        public ShooterSpatialHashGrid(float cellSize)
        {
            _cellSize = cellSize <= 0f ? 1f : cellSize;
        }

        public float CellSize => _cellSize;

        public int CellCount => _activeCellKeys.Count;

        public int TotalEntries => _totalEntries;

        public int LargestCellOccupancy => _largestCellOccupancy;

        public void Clear()
        {
            for (var i = 0; i < _activeCellKeys.Count; i++)
            {
                _cells[_activeCellKeys[i]].Reset();
            }

            _activeCellKeys.Clear();
            _largestCellOccupancy = 0;
            _totalEntries = 0;
        }

        public void Add(float x, float y, int value)
        {
            var key = ComputeCellKey(x, y);
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = new ShooterSpatialHashCell();
                _cells[key] = cell;
            }

            if (cell.Count == 0)
            {
                _activeCellKeys.Add(key);
            }
 
            cell.Add(value);
            _totalEntries++;
            if (cell.Count > _largestCellOccupancy)
            {
                _largestCellOccupancy = cell.Count;
            }
        }

        public void CollectAabb(float x, float y, float radius, List<int> target)
        {
            var minCellX = FloorToInt((x - radius) / _cellSize);
            var maxCellX = FloorToInt((x + radius) / _cellSize);
            var minCellY = FloorToInt((y - radius) / _cellSize);
            var maxCellY = FloorToInt((y + radius) / _cellSize);

            for (var cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                for (var cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    WriteCell(cellX, cellY, target);
                }
            }
        }

        public bool CollectRing(float x, float y, int radius, List<int> target)
        {
            var cellX = FloorToInt(x / _cellSize);
            var cellY = FloorToInt(y / _cellSize);
            return CollectRingByCell(cellX, cellY, radius, target);
        }

        public bool CollectRingByCell(int originCellX, int originCellY, int radius, List<int> target)
        {
            EnsureRingOffsets(radius);
            var before = target.Count;
            var range = _ringOffsetRanges[radius];
            var end = range.Start + range.Count;
            for (var i = range.Start; i < end; i++)
            {
                var offset = _ringOffsets[i];
                WriteCell(originCellX + offset.Dx, originCellY + offset.Dy, target);
            }

            return target.Count > before;
        }

        public int ComputeCellX(float x)
        {
            return FloorToInt(x / _cellSize);
        }

        public int ComputeCellY(float y)
        {
            return FloorToInt(y / _cellSize);
        }

        private void WriteCell(int cellX, int cellY, List<int> target)
        {
            if (_cells.TryGetValue(CombineCellKey(cellX, cellY), out var cell))
            {
                cell.WriteTo(target);
            }
        }

        private long ComputeCellKey(float x, float y)
        {
            return CombineCellKey(ComputeCellX(x), ComputeCellY(y));
        }

        private void EnsureRingOffsets(int maxRadius)
        {
            if (maxRadius < 0)
            {
                return;
            }

            var currentMaxRadius = _ringOffsetRanges.Count - 1;
            if (currentMaxRadius >= maxRadius)
            {
                return;
            }

            for (var radius = currentMaxRadius + 1; radius <= maxRadius; radius++)
            {
                var start = _ringOffsets.Count;
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (radius > 0 && Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        {
                            continue;
                        }

                        _ringOffsets.Add(new ShooterSpatialSearchOffset(x, y));
                    }
                }

                _ringOffsetRanges.Add(new ShooterSpatialRingOffsetRange(start, _ringOffsets.Count - start));
            }
        }

        public static long CombineCellKey(int cellX, int cellY)
        {
            unchecked
            {
                return ((long)(uint)cellX << 32) | (uint)cellY;
            }
        }

        public static int FloorToInt(float value)
        {
            return value >= 0f ? (int)value : (int)MathF.Floor(value);
        }

        private sealed class ShooterSpatialHashCell
        {
            public int Count;
            public int Id0;
            public int Id1;
            public int Id2;
            public int Id3;
            public int Id4;
            public int Id5;
            public int Id6;
            public int Id7;
            public List<int>? Overflow;

            public void Reset()
            {
                Count = 0;
                Overflow?.Clear();
            }

            public void Add(int value)
            {
                switch (Count)
                {
                    case 0: Id0 = value; break;
                    case 1: Id1 = value; break;
                    case 2: Id2 = value; break;
                    case 3: Id3 = value; break;
                    case 4: Id4 = value; break;
                    case 5: Id5 = value; break;
                    case 6: Id6 = value; break;
                    case 7: Id7 = value; break;
                    default:
                        Overflow ??= new List<int>(8);
                        Overflow.Add(value);
                        Count++;
                        return;
                }

                Count++;
            }

            public void WriteTo(List<int> target)
            {
                if (Count > 0) target.Add(Id0);
                if (Count > 1) target.Add(Id1);
                if (Count > 2) target.Add(Id2);
                if (Count > 3) target.Add(Id3);
                if (Count > 4) target.Add(Id4);
                if (Count > 5) target.Add(Id5);
                if (Count > 6) target.Add(Id6);
                if (Count > 7) target.Add(Id7);
                if (Overflow != null)
                {
                    target.AddRange(Overflow);
                }
            }
        }

        private readonly struct ShooterSpatialSearchOffset
        {
            public ShooterSpatialSearchOffset(int dx, int dy)
            {
                Dx = dx;
                Dy = dy;
            }

            public int Dx { get; }
            public int Dy { get; }
        }

        private readonly struct ShooterSpatialRingOffsetRange
        {
            public ShooterSpatialRingOffsetRange(int start, int count)
            {
                Start = start;
                Count = count;
            }

            public int Start { get; }
            public int Count { get; }
        }
    }
}
