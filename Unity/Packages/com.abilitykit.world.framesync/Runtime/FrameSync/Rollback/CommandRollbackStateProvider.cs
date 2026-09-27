using System;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// Bridges serialized rollback commands into world snapshots. Snapshot payloads contain an
    /// exact journal checkpoint, while the command parameters remain in the retained journal.
    /// </summary>
    public sealed class CommandRollbackStateProvider :
        IRollbackStructureRestoreProvider,
        IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 900001;

        private readonly CommandRollbackLog _log;
        private readonly RollbackCommandHandlerRegistry _handlers;
        private readonly IServiceProvider _services;

        public CommandRollbackStateProvider(
            CommandRollbackLog log,
            RollbackCommandHandlerRegistry handlers,
            IServiceProvider services = null,
            int key = DefaultKey)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            if (!handlers.IsSealed)
            {
                throw new InvalidOperationException(
                    "Rollback command handler registry must be sealed before creating the state provider.");
            }

            _services = services;
            Key = key;
        }

        public int Key { get; }

        public byte[] Export(FrameIndex frame)
        {
            var checkpoint = _log.CreateCheckpoint();
            return CommandJournalCheckpointCodec.Encode(in checkpoint);
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var checkpoint = CommandJournalCheckpointCodec.Decode(payload);
            _log.PrepareRollback(in checkpoint, _handlers, frame, _services);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var checkpoint = CommandJournalCheckpointCodec.Decode(payload);
            _log.RollbackTo(in checkpoint, _handlers, frame, _services);
        }
    }
}
