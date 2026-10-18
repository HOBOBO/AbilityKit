using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaSkillPipelineRollbackProvider : IRollbackStateProvider,
        IRollbackStatePreflightProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10033;
        private readonly SkillCastCoordinator _skills;

        public MobaSkillPipelineRollbackProvider(SkillCastCoordinator skills)
        {
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        }

        public int Key => DefaultKey;
        public string Name => "SkillPipelines";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void ValidateImport(FrameIndex frame, byte[] payload) => _skills.ValidateRunnerRollbackState(payload);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);
        public byte[] ExportState(FrameIndex frame) => _skills.ExportRunnerRollbackState();
        public void ImportState(FrameIndex frame, byte[] payload) => _skills.ImportRunnerRollbackState(payload);

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            foreach (var value in payload) hash.AddByte(value);
        }
    }
}
