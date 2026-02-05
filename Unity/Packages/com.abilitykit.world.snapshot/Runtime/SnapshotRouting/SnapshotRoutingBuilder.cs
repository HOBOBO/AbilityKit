using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public static class SnapshotRoutingBuilder
    {
        public static SnapshotRoutingInstance Build(object ctx, IEnumerable<ISnapshotRegistry> registries)
        {
            var snapshots = new FrameSnapshotDispatcher();
            var pipeline = new SnapshotPipeline(ctx, snapshots);
            var cmdHandler = new SnapshotCmdHandler(ctx, snapshots);

            if (registries != null)
            {
                foreach (var reg in registries)
                {
                    if (reg == null) continue;
                    reg.RegisterAll(snapshots, pipeline, pipeline, cmdHandler);
                }
            }

            return new SnapshotRoutingInstance(snapshots, pipeline, cmdHandler);
        }

        public static SnapshotRoutingInstance Build(object ctx, SnapshotRegistryCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return Build(ctx, catalog.Registries);
        }

        public static SnapshotRoutingInstance Build(object ctx, SnapshotRegistryCatalog catalog, ISet<string> enabledRegistryIds)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return Build(ctx, catalog.Registries, enabledRegistryIds);
        }

        public static SnapshotRoutingInstance Build(
            object ctx,
            IEnumerable<ISnapshotRegistry> registries,
            ISet<string> enabledRegistryIds)
        {
            var snapshots = new FrameSnapshotDispatcher();
            var pipeline = new SnapshotPipeline(ctx, snapshots);
            var cmdHandler = new SnapshotCmdHandler(ctx, snapshots);

            if (registries != null)
            {
                foreach (var reg in registries)
                {
                    if (reg == null) continue;

                    if (enabledRegistryIds != null && reg is IIdentifiedSnapshotRegistry identified)
                    {
                        if (!enabledRegistryIds.Contains(identified.RegistryId)) continue;
                    }

                    reg.RegisterAll(snapshots, pipeline, pipeline, cmdHandler);
                }
            }

            return new SnapshotRoutingInstance(snapshots, pipeline, cmdHandler);
        }

        public static SnapshotRoutingInstance Build(object ctx, params ISnapshotRegistry[] registries)
        {
            return Build(ctx, (IEnumerable<ISnapshotRegistry>)registries);
        }

        public static ISnapshotRegistry From(
            Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            return new DelegateSnapshotRegistry(register);
        }

        public static IIdentifiedSnapshotRegistry From(
            string registryId,
            Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            return new IdentifiedDelegateSnapshotRegistry(registryId, register);
        }
    }
}
