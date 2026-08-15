using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 审计迁移步骤 6“枚举收敛”决策的单一事实来源：<see cref="NetworkSyncModel"/> 保留为向后兼容的别名 key，
    /// 实际能力描述位于 <see cref="NetworkSyncProfile"/>。该注册表是唯一知道所有已知模型、规范档案与稳定显示名的位置，
    /// 因此映射关系不再需要同时维护手写 switch 与静态属性集合。
    /// </summary>
    /// <remarks>
    /// 该注册表与玩法和协议无关：它只引用帧、tick、策略层概念。游戏层可以继续通过旧 API 传入
    /// <see cref="NetworkSyncModel"/>；新逻辑应读取解析后的 <see cref="NetworkSyncProfile"/> 策略字段，而不是对别名分支。
    /// </remarks>
    public static class NetworkSyncProfileRegistry
    {
        private static readonly NetworkSyncProfileCatalog BuiltInCatalog = CreateBuiltInCatalog();

        /// <summary>
        /// 已注册兼容模型数量。
        /// </summary>
        public static int Count => BuiltInCatalog.Count;

        /// <summary>内置同步档案的冻结目录。</summary>
        public static NetworkSyncProfileCatalog DefaultCatalog => BuiltInCatalog;

        /// <summary>创建包含全部内置档案的可变目录，供接入项目继续注册或覆盖。</summary>
        public static NetworkSyncProfileCatalog CreateMutableCatalog()
        {
            return BuiltInCatalog.CreateMutableCopy();
        }

        /// <summary>
        /// 解析兼容模型对应的规范 <see cref="NetworkSyncProfile"/>。
        /// 对未知模型抛出 <see cref="ArgumentOutOfRangeException"/>，避免调用方静默使用空档案运行。
        /// </summary>
        public static NetworkSyncProfile Resolve(NetworkSyncModel model)
        {
            return BuiltInCatalog.Resolve(model);
        }

        /// <summary>
        /// 尝试解析兼容模型对应的规范档案，不抛出异常。
        /// </summary>
        public static bool TryResolve(NetworkSyncModel model, out NetworkSyncProfile profile)
        {
            return BuiltInCatalog.TryResolve(model, out profile);
        }

        /// <summary>
        /// 返回兼容模型的稳定显示名（与枚举成员名一致）。对未知模型抛出 <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public static string GetName(NetworkSyncModel model)
        {
            return BuiltInCatalog.GetName(model);
        }

        /// <summary>
        /// 按枚举顺序遍历所有已注册兼容模型。
        /// </summary>
        public static IEnumerable<NetworkSyncModel> Models()
        {
            var entries = BuiltInCatalog.Entries();
            for (var i = 0; i < entries.Count; i++)
            {
                yield return entries[i].Model;
            }
        }

        /// <summary>
        /// 按枚举顺序遍历所有已注册档案。适合在不手写每个 profile 的情况下构建能力矩阵。
        /// </summary>
        public static IEnumerable<NetworkSyncProfile> Profiles()
        {
            var entries = BuiltInCatalog.Entries();
            for (var i = 0; i < entries.Count; i++)
            {
                yield return entries[i].Profile;
            }
        }

        private static NetworkSyncProfileCatalog CreateBuiltInCatalog()
        {
            var catalog = new NetworkSyncProfileCatalog();
            catalog.Register(nameof(NetworkSyncModel.Unspecified), NetworkSyncProfiles.Unspecified);
            catalog.Register(nameof(NetworkSyncModel.Lockstep), NetworkSyncProfiles.Lockstep);
            catalog.Register(nameof(NetworkSyncModel.PredictRollback), NetworkSyncProfiles.PredictRollback);
            catalog.Register(nameof(NetworkSyncModel.AuthoritativeInterpolation), NetworkSyncProfiles.AuthoritativeInterpolation);
            catalog.Register(nameof(NetworkSyncModel.BatchStateSync), NetworkSyncProfiles.BatchStateSync);
            catalog.Register(nameof(NetworkSyncModel.MassBattleLodSync), NetworkSyncProfiles.MassBattleLodSync);
            catalog.Register(nameof(NetworkSyncModel.HybridHeroPrediction), NetworkSyncProfiles.HybridHeroPrediction);
            catalog.Register(nameof(NetworkSyncModel.FastReconnect), NetworkSyncProfiles.FastReconnect);
            catalog.Register(nameof(NetworkSyncModel.ServerRewindLagCompensation), NetworkSyncProfiles.ServerRewindLagCompensation);
            catalog.Freeze();
            return catalog;
        }
    }
}
