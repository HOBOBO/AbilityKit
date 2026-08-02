using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Coordinator.Core;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 同步适配器工厂。
    ///
    /// 设计：
    /// - 根据配置创建合适的同步适配器。
    /// - 集中管理适配器实例化逻辑。
    /// - 为不同模式的行为返回强类型接口。
    /// </summary>
    public delegate ISyncAdapter SyncAdapterCreator(IWorld world, in SessionConfig config);

    public interface ISyncAdapterFactory
    {
        ISyncAdapter Create(IWorld world, in SessionConfig config);
    }

    public sealed class DefaultSyncAdapterFactory : ISyncAdapterFactory
    {
        private readonly Dictionary<Core.SyncMode, SyncAdapterCreator> _creators = new Dictionary<Core.SyncMode, SyncAdapterCreator>();

        public DefaultSyncAdapterFactory()
        {
            Register(Core.SyncMode.Lockstep, (IWorld world, in SessionConfig config) => new LocalSyncAdapter(world, config));
            Register(Core.SyncMode.SnapshotAuthority, (IWorld world, in SessionConfig config) => new RemoteSyncAdapter(world, config));
            Register(Core.SyncMode.StateSync, (IWorld world, in SessionConfig config) => new RemoteSyncAdapter(world, config));
            Register(Core.SyncMode.Hybrid, (IWorld world, in SessionConfig config) => new RemoteSyncAdapter(world, config));
        }

        public DefaultSyncAdapterFactory Register(Core.SyncMode mode, SyncAdapterCreator creator)
        {
            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            _creators[mode] = creator;
            return this;
        }

        public ISyncAdapter Create(IWorld world, in SessionConfig config)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world), "World cannot be null");
            }

            var policy = config.ResolveRuntimePolicy();
            if (_creators.TryGetValue(policy.EffectiveSyncMode, out var creator))
            {
                return creator(world, in config);
            }

            return new LocalSyncAdapter(world, config);
        }
    }

    public static class SyncAdapterFactory
    {
        private static ISyncAdapterFactory _defaultFactory = new DefaultSyncAdapterFactory();

        public static ISyncAdapterFactory DefaultFactory => _defaultFactory;

        public static void SetDefaultFactory(ISyncAdapterFactory factory)
        {
            _defaultFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static void ResetDefaultFactory()
        {
            _defaultFactory = new DefaultSyncAdapterFactory();
        }

        /// <summary>
        /// 根据同步模式创建同步适配器。
        /// </summary>
        public static ISyncAdapter Create(IWorld world, in SessionConfig config)
        {
            return _defaultFactory.Create(world, in config);
        }

        /// <summary>
        /// 创建本地同步适配器（Lockstep 模式）。
        /// </summary>
        public static ILocalSyncAdapter CreateLocalSyncAdapter(IWorld world, in SessionConfig config)
        {
            return new LocalSyncAdapter(world, config);
        }

        /// <summary>
        /// 创建远程同步适配器（服务器权威模式）。
        /// </summary>
        public static IRemoteSyncAdapter CreateRemoteSyncAdapter(IWorld world, in SessionConfig config)
        {
            return new RemoteSyncAdapter(world, config);
        }

        /// <summary>
        /// Coordinator 不再提供混合预测适配器；预测由玩法专用运行时模块负责。
        /// </summary>
        [Obsolete("Use ClientPredictionDriverModule for MOBA framesync or ShooterClientPredictionRuntimeAdapter for Shooter statesync.")]
        public static IPredictionSyncAdapter CreateHybridSyncAdapter(IWorld world, in SessionConfig config)
        {
            throw new NotSupportedException(
                "Coordinator no longer provides a hybrid prediction adapter. " +
                "Use ClientPredictionDriverModule for MOBA framesync or " +
                "ShooterClientPredictionRuntimeAdapter for Shooter statesync.");
        }

        // ============== 工具方法 ==============

        /// <summary>
        /// 根据模式获取同步适配器类型名。
        /// </summary>
        public static string GetAdapterTypeName(Core.SyncMode mode)
        {
            return mode switch
            {
                Core.SyncMode.Lockstep => nameof(LocalSyncAdapter),
                Core.SyncMode.SnapshotAuthority => nameof(RemoteSyncAdapter),
                Core.SyncMode.StateSync => nameof(RemoteSyncAdapter),
                Core.SyncMode.Hybrid => nameof(RemoteSyncAdapter),
                _ => nameof(LocalSyncAdapter)
            };
        }

        /// <summary>
        /// 检查同步适配器是否需要网络连接。
        /// </summary>
        public static bool RequiresNetwork(Core.SyncMode mode)
        {
            return mode switch
            {
                Core.SyncMode.Lockstep => false,
                Core.SyncMode.SnapshotAuthority => true,
                Core.SyncMode.StateSync => true,
                Core.SyncMode.Hybrid => true,
                _ => false
            };
        }

        /// <summary>
        /// Coordinator 适配器本身不提供客户端预测。
        /// </summary>
        public static bool SupportsPrediction(Core.SyncMode mode)
        {
            return false;
        }
    }
}
