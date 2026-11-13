using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Rollback adapter for lifecycle-aware Buff recovery. The later key places
    /// Buff reconstruction after execution contexts and skill runtimes have been
    /// restored, so Buff bindings can safely reattach to their owners.
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaBuffLifecycleRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = MobaBuffStateRecoveryProvider.DefaultKey;
        private readonly MobaBuffStateRecoveryProvider _recovery;

        public MobaBuffLifecycleRollbackProvider(MobaBuffStateRecoveryProvider recovery)
        {
            _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        }

        public int Key => DefaultKey;
        public string Name => "BuffLifecycle";

        public byte[] Export(FrameIndex frame) => _recovery.ExportState(frame);

        public void ValidateImport(FrameIndex frame, byte[] payload) => _recovery.PrepareRestore(frame, payload);

        public void Import(FrameIndex frame, byte[] payload)
        {
            _recovery.ImportState(frame, payload);
            _recovery.ValidateRestoredState(frame, payload);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash) => _recovery.AddStateHash(frame, ref hash);
    }
}
