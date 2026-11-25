#nullable enable annotations

using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Snapshots.Routing
{
    /// <summary>
    /// 按注册顺序装配快照派发、处理阶段和命令订阅。
    /// </summary>
    public static class SnapshotRoutingBuilder
    {
        /// <summary>
        /// 创建并装配由路由实例持有的派发器。
        /// </summary>
        public static SnapshotRoutingInstance Build(object ctx, IEnumerable<ISnapshotRegistry> registries)
        {
            return Build(ctx, registries, null);
        }

        /// <summary>
        /// 借用调用方的派发器装配路由，不接管派发器的生命周期。
        /// </summary>
        /// <remarks>
        /// 装配失败时撤销本次装配持有的订阅，但不回滚已写入外部派发器的解码器注册。
        /// </remarks>
        public static SnapshotRoutingInstance Build(object ctx, ISnapshotDispatcher externalDispatcher, IEnumerable<ISnapshotRegistry> registries)
        {
            return Build(ctx, externalDispatcher, registries, null);
        }

        /// <summary>
        /// 借用调用方的派发器，按启用标识筛选有标识的注册项；无标识的注册项始终参与装配。
        /// </summary>
        /// <remarks>
        /// 装配失败时撤销本次装配持有的订阅，但不回滚已写入外部派发器的解码器注册。
        /// </remarks>
        public static SnapshotRoutingInstance Build(
            object ctx,
            ISnapshotDispatcher externalDispatcher,
            IEnumerable<ISnapshotRegistry> registries,
            ISet<string>? enabledRegistryIds)
        {
            if (externalDispatcher == null) throw new ArgumentNullException(nameof(externalDispatcher));

            return BuildCore(ctx, externalDispatcher, registries, enabledRegistryIds, ownedDispatcher: null);
        }

        /// <summary>
        /// 按目录中的注册顺序装配路由和自有派发器。
        /// </summary>
        public static SnapshotRoutingInstance Build(object ctx, SnapshotRegistryCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return Build(ctx, catalog.Registries);
        }

        /// <summary>
        /// 从目录筛选启用的注册项，并装配路由和自有派发器。
        /// </summary>
        public static SnapshotRoutingInstance Build(object ctx, SnapshotRegistryCatalog catalog, ISet<string>? enabledRegistryIds)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return Build(ctx, catalog.Registries, enabledRegistryIds);
        }

        /// <summary>
        /// 创建自有派发器，装配启用的有标识注册项和全部无标识注册项。
        /// </summary>
        public static SnapshotRoutingInstance Build(
            object ctx,
            IEnumerable<ISnapshotRegistry> registries,
            ISet<string>? enabledRegistryIds)
        {
            var snapshots = new FrameSnapshotDispatcher();
            return BuildCore(ctx, snapshots, registries, enabledRegistryIds, ownedDispatcher: snapshots);
        }

        private static SnapshotRoutingInstance BuildCore(
            object ctx,
            ISnapshotDispatcher dispatcher,
            IEnumerable<ISnapshotRegistry>? registries,
            ISet<string>? enabledRegistryIds,
            FrameSnapshotDispatcher? ownedDispatcher)
        {
            var pipeline = new SnapshotPipeline(ctx, dispatcher);
            var cmdHandler = new SnapshotCmdHandler(ctx, dispatcher);
            var routing = ownedDispatcher != null
                ? new SnapshotRoutingInstance(ownedDispatcher, pipeline, cmdHandler)
                : new SnapshotRoutingInstance(dispatcher, pipeline, cmdHandler);

            try
            {
                if (registries == null) return routing;

                foreach (var registry in registries)
                {
                    if (registry == null) continue;

                    if (enabledRegistryIds != null && registry is IIdentifiedSnapshotRegistry identified)
                    {
                        if (!enabledRegistryIds.Contains(identified.RegistryId)) continue;
                    }

                    registry.RegisterAll(dispatcher, pipeline, pipeline, cmdHandler);
                }

                return routing;
            }
            catch
            {
                routing.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 按给定顺序装配注册项和自有派发器。
        /// </summary>
        public static SnapshotRoutingInstance Build(object ctx, params ISnapshotRegistry[] registries)
        {
            return Build(ctx, (IEnumerable<ISnapshotRegistry>)registries);
        }

        /// <summary>
        /// 以委托创建无标识的注册项，不参与标识筛选。
        /// </summary>
        public static ISnapshotRegistry From(
            Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            return new DelegateSnapshotRegistry(register);
        }

        /// <summary>
        /// 以委托创建可按标识筛选的注册项。
        /// </summary>
        public static IIdentifiedSnapshotRegistry From(
            string registryId,
            Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            return new IdentifiedDelegateSnapshotRegistry(registryId, register);
        }
    }
}
