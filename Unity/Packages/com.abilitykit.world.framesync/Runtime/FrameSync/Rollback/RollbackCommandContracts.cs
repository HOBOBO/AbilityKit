using System;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// Immutable description of a recorded inverse command.
    /// </summary>
    public readonly struct RollbackCommandRecord
    {
        private readonly byte[] _payload;

        internal RollbackCommandRecord(
            FrameIndex frame,
            long order,
            int commandType,
            int payloadVersion,
            byte[] payload)
        {
            Frame = frame;
            Order = order;
            CommandType = commandType;
            PayloadVersion = payloadVersion;
            _payload = payload ?? Array.Empty<byte>();
        }

        public FrameIndex Frame { get; }

        public long Order { get; }

        public int CommandType { get; }

        public int PayloadVersion { get; }

        public ReadOnlyMemory<byte> Payload => _payload ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Framework-owned data passed to a rollback command handler. Business services are resolved
    /// by stable identity from <see cref="Services"/>; recorded object instances must not be captured.
    /// </summary>
    public readonly struct RollbackCommandContext
    {
        internal RollbackCommandContext(
            FrameIndex targetFrame,
            FrameIndex commandFrame,
            long commandOrder,
            IServiceProvider services)
        {
            TargetFrame = targetFrame;
            CommandFrame = commandFrame;
            CommandOrder = commandOrder;
            Services = services;
        }

        public FrameIndex TargetFrame { get; }

        public FrameIndex CommandFrame { get; }

        public long CommandOrder { get; }

        public IServiceProvider Services { get; }

        public bool IsRestoring => true;

        public T GetRequiredService<T>() where T : class
        {
            var service = Services?.GetService(typeof(T)) as T;
            if (service != null) return service;

            throw new InvalidOperationException(
                $"Rollback command service is not registered: {typeof(T).FullName}");
        }
    }

    /// <summary>
    /// Stable executor for one rollback command type.
    /// </summary>
    public interface IRollbackCommandHandler
    {
        int CommandType { get; }

        bool CanRollback(int payloadVersion);

        /// <summary>
        /// Validates payload shape and stable references without mutating runtime state.
        /// </summary>
        void ValidateRollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload);

        void Rollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload);
    }
}
