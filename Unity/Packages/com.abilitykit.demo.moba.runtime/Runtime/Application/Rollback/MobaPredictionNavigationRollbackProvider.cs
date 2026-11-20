using System;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services.Navigation;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 验证预测快照依赖的导航输入，不导入地图，也不执行寻路。
    /// </summary>
    internal sealed class MobaPredictionNavigationRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        internal const int DefaultKey = 10048;
        private const int Version = 1;
        private readonly MobaNavigationService _navigation;
        private byte[] _cached;
        private MobaNavigationService.FrozenNavigationWorld _cachedWorld;
        private bool _cachedBuildCompleted;

        internal MobaPredictionNavigationRollbackProvider(MobaNavigationService navigation) => _navigation = navigation;

        /// <summary>
        /// 预测 Registry 的独立导航依赖键。
        /// </summary>
        public int Key => DefaultKey;

        /// <summary>
        /// 提供导航依赖的诊断名称。
        /// </summary>
        public string Name => "PredictionNavigation";

        /// <summary>
        /// 导出独立值载荷，不转移内部缓冲区所有权。
        /// </summary>
        public byte[] Export(FrameIndex frame) => (byte[])CanonicalPayload().Clone();

        /// <summary>
        /// 在导入世界前验证导航输入与目标帧完全相同。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            if (payload == null || !CanonicalPayload().AsSpan().SequenceEqual(payload))
                throw new InvalidOperationException("Prediction navigation input differs from the target snapshot.");
        }

        /// <summary>
        /// 静态输入无需写回，只重验依赖。
        /// </summary>
        public void Import(FrameIndex frame, byte[] payload) => ValidateImport(frame, payload);

        private byte[] CanonicalPayload()
        {
            var world = _navigation.GetPredictionInput(out var buildCompleted);
            if (_cached != null && _cachedBuildCompleted == buildCompleted && ReferenceEquals(_cachedWorld, world)) return _cached;

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(buildCompleted);
            writer.Write(world != null);
            if (world != null)
            {
                var grid = world.Grid;
                var options = world.CopyOptions();
                writer.Write(grid.Origin.X);
                writer.Write(grid.Origin.Y);
                writer.Write(grid.Origin.Z);
                writer.Write(grid.CellSize);
                writer.Write(grid.Width);
                writer.Write(grid.Height);
                for (var index = 0; index < grid.CellCount; index++) writer.Write(grid.IsBlocked(index));
                writer.Write(options.CellSize);
                writer.Write(options.AgentRadius);
                writer.Write(options.AllowDiagonal);
                writer.Write(options.MaxIterations);
                writer.Write(options.SimplifyPath);
            }
            _cachedWorld = world;
            _cachedBuildCompleted = buildCompleted;
            return _cached = stream.ToArray();
        }
    }
}
