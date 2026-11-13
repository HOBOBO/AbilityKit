using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>Single local owner of skill lifecycle structure. Versioned checkpoints
    /// deliberately reject the old context-only payload previously using this registry key.</summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaSkillLifecycleRollbackProvider : IRollbackStructureRestoreProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10011;
        private readonly MobaSkillLifecycleRollbackPreparation _lifecycle;
        internal MobaOwnerBlackboardRollbackProvider OwnerBlackboards { get; }

        public MobaSkillLifecycleRollbackProvider(IWorld world, MobaOwnerBlackboardRollbackProvider owners)
        {
            OwnerBlackboards = owners ?? throw new ArgumentNullException(nameof(owners));
            _lifecycle = new MobaSkillLifecycleRollbackPreparation(world, owners);
        }

        public int Key => DefaultKey;
        public byte[] Export(FrameIndex frame) => _lifecycle.Capture(frame);
        public void ValidateImport(FrameIndex frame, byte[] payload) => _lifecycle.Prepare(frame, payload);
        public void Import(FrameIndex frame, byte[] payload) => _lifecycle.Commit(_lifecycle.Prepare(frame, payload));
    }
}
