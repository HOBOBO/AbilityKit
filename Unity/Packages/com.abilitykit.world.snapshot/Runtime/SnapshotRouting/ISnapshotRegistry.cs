using System;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    public interface ISnapshotRegistry
    {
        void RegisterAll(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd);
    }

    public interface IIdentifiedSnapshotRegistry : ISnapshotRegistry
    {
        string RegistryId { get; }
    }

    public sealed class DelegateSnapshotRegistry : ISnapshotRegistry
    {
        private readonly Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> _register;

        public DelegateSnapshotRegistry(Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            _register = register ?? throw new ArgumentNullException(nameof(register));
        }

        public void RegisterAll(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd)
        {
            _register(dispatcherDecoders, pipelineDecoders, pipeline, cmd);
        }
    }

    public sealed class IdentifiedDelegateSnapshotRegistry : IIdentifiedSnapshotRegistry
    {
        private readonly Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> _register;

        public IdentifiedDelegateSnapshotRegistry(
            string registryId,
            Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            RegistryId = registryId ?? throw new ArgumentNullException(nameof(registryId));
            _register = register ?? throw new ArgumentNullException(nameof(register));
        }

        public string RegistryId { get; }

        public void RegisterAll(
            ISnapshotDecoderRegistry dispatcherDecoders,
            ISnapshotDecoderRegistry pipelineDecoders,
            ISnapshotPipelineStageRegistry pipeline,
            ISnapshotCmdHandlerRegistry cmd)
        {
            _register(dispatcherDecoders, pipelineDecoders, pipeline, cmd);
        }
    }
}
