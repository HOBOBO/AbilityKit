using System;
using System.Collections.Generic;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 基于网格的空间划分广相算法
    /// </summary>
    public sealed class GridBroadphase : IBroadphase
    {
        private readonly float _cellSize;
        private readonly int _poolSize;
        private readonly Dictionary<int, CellEntry> _colliderToCell;
        private readonly Dictionary<long, List<int>> _cells;
        private readonly List<int> _tmpResults;

        private struct CellEntry
        {
            public int MinX;
            public int MinY;
            public int MinZ;
            public int MaxX;
            public int MaxY;
            public int MaxZ;

            public bool IsValid;

            public bool SameRange(int mnX, int mnY, int mnZ, int mxX, int mxY, int mxZ)
                => MinX == mnX && MinY == mnY && MinZ == mnZ && MaxX == mxX && MaxY == mxY && MaxZ == mxZ;
        }

        public GridBroadphase(float cellSize = 4f, int poolSize = 1024)
        {
            _cellSize = cellSize;
            _poolSize = poolSize;
            _colliderToCell = new Dictionary<int, CellEntry>(poolSize);
            _cells = new Dictionary<long, List<int>>(poolSize);
            _tmpResults = new List<int>(64);
        }

        public void Clear()
        {
            _colliderToCell.Clear();
            _cells.Clear();
        }

        public void Update(int colliderId, in Core.Mathematics.Aabb worldAabb)
        {
            var center = worldAabb.Center;
            var extents = worldAabb.Extents * 0.5f;

            var minCX = WorldToCell(center.X - extents.X);
            var minCY = WorldToCell(center.Y - extents.Y);
            var minCZ = WorldToCell(center.Z - extents.Z);
            var maxCX = WorldToCell(center.X + extents.X);
            var maxCY = WorldToCell(center.Y + extents.Y);
            var maxCZ = WorldToCell(center.Z + extents.Z);

            bool exists = _colliderToCell.TryGetValue(colliderId, out var oldEntry);

            if (exists && oldEntry.IsValid)
            {
                if (oldEntry.SameRange(minCX, minCY, minCZ, maxCX, maxCY, maxCZ))
                {
                    return;
                }

                RemoveFromRange(colliderId, in oldEntry);
            }

            for (var cx = minCX; cx <= maxCX; cx++)
            {
                for (var cy = minCY; cy <= maxCY; cy++)
                {
                    for (var cz = minCZ; cz <= maxCZ; cz++)
                    {
                        var key = GetCellKey(cx, cy, cz);
                        if (!_cells.TryGetValue(key, out var list))
                        {
                            list = new List<int>(4);
                            _cells[key] = list;
                        }
                        list.Add(colliderId);
                    }
                }
            }

            _colliderToCell[colliderId] = new CellEntry
            {
                MinX = minCX,
                MinY = minCY,
                MinZ = minCZ,
                MaxX = maxCX,
                MaxY = maxCY,
                MaxZ = maxCZ,
                IsValid = true
            };
        }

        public void Remove(int colliderId)
        {
            if (!_colliderToCell.TryGetValue(colliderId, out var entry) || !entry.IsValid)
                return;

            RemoveFromRange(colliderId, in entry);
            _colliderToCell.Remove(colliderId);
        }

        private void RemoveFromRange(int colliderId, in CellEntry entry)
        {
            for (var cx = entry.MinX; cx <= entry.MaxX; cx++)
            {
                for (var cy = entry.MinY; cy <= entry.MaxY; cy++)
                {
                    for (var cz = entry.MinZ; cz <= entry.MaxZ; cz++)
                    {
                        var key = GetCellKey(cx, cy, cz);
                        if (_cells.TryGetValue(key, out var list))
                        {
                            list.Remove(colliderId);
                            if (list.Count == 0)
                                _cells.Remove(key);
                        }
                    }
                }
            }
        }

        public int Query(in Core.Mathematics.Aabb queryAabb, int[] results, int maxResults)
        {
            if (results == null || maxResults <= 0)
                return 0;

            var center = queryAabb.Center;
            var extents = queryAabb.Extents * 0.5f;

            var minCX = WorldToCell(center.X - extents.X);
            var minCY = WorldToCell(center.Y - extents.Y);
            var minCZ = WorldToCell(center.Z - extents.Z);
            var maxCX = WorldToCell(center.X + extents.X);
            var maxCY = WorldToCell(center.Y + extents.Y);
            var maxCZ = WorldToCell(center.Z + extents.Z);

            _tmpResults.Clear();

            for (var cx = minCX; cx <= maxCX; cx++)
            {
                for (var cy = minCY; cy <= maxCY; cy++)
                {
                    for (var cz = minCZ; cz <= maxCZ; cz++)
                    {
                        var key = GetCellKey(cx, cy, cz);
                        if (_cells.TryGetValue(key, out var list))
                        {
                            for (var i = 0; i < list.Count; i++)
                            {
                                var id = list[i];
                                if (!_tmpResults.Contains(id))
                                    _tmpResults.Add(id);
                            }
                        }
                    }
                }
            }

            var count = 0;
            for (var i = 0; i < _tmpResults.Count && count < maxResults; i++)
            {
                results[count++] = _tmpResults[i];
            }

            return count;
        }

        private int WorldToCell(float worldCoord)
        {
            return (int)System.Math.Floor(worldCoord / _cellSize);
        }

        private static long GetCellKey(int cx, int cy, int cz)
        {
            return ((long)cx << 42) | ((long)cy << 21) | (long)cz;
        }
    }
}
