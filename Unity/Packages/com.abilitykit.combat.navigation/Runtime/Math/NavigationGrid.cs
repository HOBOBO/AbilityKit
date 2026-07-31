using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 均匀方格导航网格。XZ 平面布点，Y 仅记录烘焙高度。
    ///
    /// 坐标约定：Origin 为 cell(0,0) 的最小角（min X / min Z）世界坐标；
    /// cell(cx,cz) 占据 X ∈ [Origin.X + cx*CellSize, Origin.X + (cx+1)*CellSize]、Z 同理；
    /// 行主序存储，index = cz * Width + cx。
    ///
    /// 纯数据，烘焙后只读，可序列化/快照。
    /// </summary>
    public sealed class NavigationGrid
    {
        public Vec3 Origin { get; }
        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly bool[] _blocked;

        public NavigationGrid(Vec3 origin, float cellSize, int width, int height, bool[] blocked)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            if (blocked == null || blocked.Length != width * height)
            {
                throw new ArgumentException("Blocked array length must equal width * height.", nameof(blocked));
            }

            Origin = origin;
            CellSize = cellSize;
            Width = width;
            Height = height;
            _blocked = blocked;
        }

        public int CellCount => Width * Height;
        public float WorldSizeX => Width * CellSize;
        public float WorldSizeZ => Height * CellSize;

        public int Index(int cx, int cz) => cz * Width + cx;

        public bool IsInBounds(int cx, int cz) => (uint)cx < (uint)Width && (uint)cz < (uint)Height;

        public bool IsBlocked(int cx, int cz) => _blocked[Index(cx, cz)];

        public bool IsBlocked(int index) => _blocked[index];

        /// <summary>cell 中心世界坐标。</summary>
        public Vec3 CellCenter(int cx, int cz)
        {
            return new Vec3(
                Origin.X + (cx + 0.5f) * CellSize,
                Origin.Y,
                Origin.Z + (cz + 0.5f) * CellSize);
        }

        /// <summary>世界坐标 → cell 索引（可能越界，调用方需 IsInBounds 校验）。</summary>
        public void WorldToCell(in Vec3 world, out int cx, out int cz)
        {
            cx = (int)Math.Floor((world.X - Origin.X) / CellSize);
            cz = (int)Math.Floor((world.Z - Origin.Z) / CellSize);
        }

        /// <summary>世界坐标 → 最近 in-bounds cell。</summary>
        public void WorldToCellClamped(in Vec3 world, out int cx, out int cz)
        {
            WorldToCell(in world, out cx, out cz);
            cx = ClampInt(cx, 0, Width - 1);
            cz = ClampInt(cz, 0, Height - 1);
        }

        private static int ClampInt(int v, int min, int max)
        {
            if (v < min) return min;
            return v > max ? max : v;
        }
    }
}
